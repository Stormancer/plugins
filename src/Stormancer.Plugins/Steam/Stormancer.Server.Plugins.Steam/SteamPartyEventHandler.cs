// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    internal class SteamUserData
    {
        public SessionId SessionId { get; set; }

        public ulong SteamId { get; set; }
    }

    internal class SteamPartyData
    {
        public TaskQueue TaskQueue { get; } = new();

        public ulong SteamIDLobby { get; set; } = 0;

        public int NumMembers { get => _numMembers; }

        // UserData[SessionId] => SteamUserData
        public ConcurrentDictionary<SessionId, SteamUserData> UserData { get; set; } = new();

        public int IncrementNumMembers()
        {
            return Interlocked.Increment(ref _numMembers);
        }

        public int DecrementNumMembers()
        {
            return Interlocked.Decrement(ref _numMembers);
        }

        private int _numMembers = 0;
        public bool IsJoinable { get; set; }
    }

    /// <summary>
    /// Steam Party Event Handler.
    /// </summary>
    [Priority(int.MinValue)]
    public class SteamPartyEventHandler : IPartyEventHandler
    {
        private static readonly string LobbyPrefix = "Party-";
        private const string PartyLobbyKey = "steam.lobby";

        private readonly RpcService _rpc;
        private readonly IUserSessions _userSessions;
        private readonly ISteamService _steamService;
        private readonly ILogger _logger;
        private readonly ISerializer _serializer;
        private readonly IServiceLocator _serviceLocator;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Steam Party Event Handler contructor.
        /// </summary>
        /// <param name="sessions"></param>
        /// <param name="steam"></param>
        /// <param name="logger"></param>
        /// <param name="rpc"></param>
        /// <param name="serializer"></param>
        /// <param name="locator"></param>
        /// <param name="configuration"></param>
        public SteamPartyEventHandler(
            IUserSessions sessions,
            ISteamService steam,
            ILogger logger,
            RpcService rpc,
            ISerializer serializer,
            IServiceLocator locator,
            IConfiguration configuration
        )
        {
            _rpc = rpc;
            _userSessions = sessions;
            _steamService = steam;
            _logger = logger;
            _serializer = serializer;
            _serviceLocator = locator;
            _configuration = configuration;
        }

        /// <summary>
        /// Steam behavior on party creating.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnCreatingParty(PartyCreationContext ctx)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Steam behavior on party join denied.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task OnJoinDenied(JoinDeniedContext ctx)
        {
            await RemoveUserFromLobby(ctx.Party.ServerData, ctx.Session.SessionId);
        }

        /// <summary>
        /// Steam behavior on party joined.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnJoined(JoinedPartyContext ctx)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Steam behavior on party joining.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task OnJoining(JoiningPartyContext ctx)
        {
            if (ctx.Session.platformId.Platform != SteamConstants.PLATFORM_NAME)
            {
                return;
            }

            if (ctx.Accept)
            {
                if (ctx.Party.Settings.ServerSettings.ShouldCreateSteamLobby() == true)
                {
                    var data = (SteamPartyData)ctx.Party.ServerData.GetOrAdd(PartyLobbyKey, new SteamPartyData());

                    await data.TaskQueue.PushWork(async () =>
                    {
                        if (ctx.Session.User == null)
                        {
                            return;
                        }

                        try
                        {
                            // Get steamId
                            var steamId = ctx.Session.User.GetSteamId();

                            if (steamId == null)
                            {
                                throw new AggregateException("SteamId is invalid");
                            }

                            // If the Steam lobby does not exist, we create it
                            if (data.SteamIDLobby == 0)
                            {
                                var lobbyName = $"{LobbyPrefix}{ctx.Party.Settings.PartyId}";
                                var joinable = ctx.Party.Settings.IsJoinable;
                                
                                var maxMembers = ctx.Party.Settings.ServerSettings.SteamMaxMembers() ?? 5;
                                var lobbyType = ctx.Party.Settings.ServerSettings.SteamLobbyType() ?? LobbyType.FriendsOnly;

                                _logger.Log(LogLevel.Trace, "SteamPartyEventHandler.OnJoining", "Creating steam lobby...", new
                                {
                                    ctx.Party.Settings.PartyId,
                                    UserId = ctx.Session.User.Id,
                                    ctx.Session.SessionId
                                });

                                var partyDataBearerToken = await _steamService.CreatePartyDataBearerToken(ctx.Party.Settings.PartyId, ctx.Session.User.Id, (ulong)steamId);
                                var createLobbyParameters = new CreateLobbyDto
                                {
                                    LobbyType = lobbyType,
                                    MaxMembers = maxMembers,
                                    Joinable = joinable,
                                    Metadata = new Dictionary<string, string> { { "partyDataToken", partyDataBearerToken } }
                                };
                                var steamIDLobby = await _userSessions.SendRequest<ulong, CreateLobbyDto>("Steam.CreateLobby", "", ctx.Session.User.Id,createLobbyParameters, CancellationToken.None);

                                if (steamIDLobby != 0)
                                {
                                    _logger.Log(LogLevel.Trace, "SteamPartyEventHandler.OnJoining", "Steam lobby created by client", new
                                    {
                                        steamIDLobby,
                                        ctx.Party.Settings.PartyId,
                                        UserId = ctx.Session.User.Id,
                                        ctx.Session.SessionId
                                    });

                                    var partySettingsDto = new PartySettingsDto(ctx.Party.State);
                                    if (partySettingsDto.PublicServerData == null)
                                    {
                                        partySettingsDto.PublicServerData = new();
                                    }
                                    partySettingsDto.PublicServerData["SteamIDLobby"] = steamIDLobby.ToString();
                                    _ = ctx.Party.UpdateSettings(partySettingsDto, CancellationToken.None);
                                    data.SteamIDLobby = steamIDLobby;
                                    data.IsJoinable = joinable;
                                }
                                else
                                {
                                    _logger.Log(LogLevel.Error, "SteamPartyEventHandler.OnJoining", "Steam lobby creation failed", new
                                    {
                                        ctx.Party.Settings.PartyId,
                                        UserId = ctx.Session.User.Id,
                                        ctx.Session.SessionId
                                    });
                                    ctx.Accept = false;
                                }
                            }
                            // else we only join the Steam lobby
                            else
                            {
                                if (data.SteamIDLobby == 0)
                                {
                                    _logger.Log(LogLevel.Error, "SteamPartyEventHandler.OnJoining", "Missing SteamIDLobby", new
                                    {
                                        ctx.Party.Settings.PartyId,
                                        UserId = ctx.Session.User.Id,
                                        ctx.Session.SessionId
                                    });
                                    ctx.Accept = false;
                                }
                                else
                                {
                                    var joinLobbyParameter = new JoinLobbyDto { SteamIDLobby = data.SteamIDLobby };
                                    await _userSessions.SendRequest("Steam.JoinLobby", "", ctx.Session.User.Id, joinLobbyParameter, CancellationToken.None);

                                    data.UserData[ctx.Session.SessionId] = new SteamUserData { SessionId = ctx.Session.SessionId, SteamId = (ulong)steamId };

                                    data.IncrementNumMembers();
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            _logger.Log(LogLevel.Error, "SteamPartyEventHandler.OnJoining", "Steam lobby creation or join failed", new
                            {
                                exception,
                                ctx.Party.Settings.PartyId,
                                UserId = ctx.Session.User.Id,
                                ctx.Session.SessionId
                            });
                            ctx.Accept = false;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Steam behavior on party player ready state changed.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnPlayerReadyStateChanged(PlayerReadyStateContext ctx)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Steam behavior on party quit.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task OnQuit(QuitPartyContext ctx)
        {
            await RemoveUserFromLobby(ctx.Party.ServerData, ctx.Args.Peer.SessionId);
        }

        /// <summary>
        /// Steam behavior on party settings update.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnUpdateSettings(PartySettingsUpdateCtx ctx)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Steam behavior on party updating settings.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnUpdatingSettings(PartySettingsUpdateCtx ctx)
        {
            return Task.CompletedTask;
        }

        private async Task RemoveUserFromLobby(ConcurrentDictionary<string, object> serverData, SessionId sessionId)
        {
            if (serverData.TryGetValue(PartyLobbyKey, out var dataObject))
            {
                var data = (SteamPartyData)dataObject;
                if (data != null && data.SteamIDLobby != 0 && data.UserData.TryGetValue(sessionId, out var steamUserData))
                {
                    await data.TaskQueue.PushWork(async () =>
                    {
                        await _steamService.RemoveUserFromLobby(steamUserData.SteamId, data.SteamIDLobby);
                        data.DecrementNumMembers();
                        data.UserData.TryRemove(sessionId, out var _);

                        if(data.UserData.Count == 0)
                        {
                            data.SteamIDLobby = 0;
                        }
                    });
                }
            }
        }
    }
}
