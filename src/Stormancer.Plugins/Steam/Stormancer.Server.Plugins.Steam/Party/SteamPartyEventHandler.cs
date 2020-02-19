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
using WindJammers2Server.Plugins.Steam.Dto;

namespace Stormancer.Server.Plugins.Steam
{
    internal class SteamUserData
    {
        public string? SessionId { get; set; }

        public ulong SteamId { get; set; }
    }

    internal class SteamPartyData
    {
        public TaskQueue TaskQueue { get; } = new TaskQueue();

        public ulong SteamIdLobby { get; set; } = 0;

        public int NumMembers { get => _numMembers; }

        // UserData[SessionId] => SteamUserData
        public ConcurrentDictionary<string, SteamUserData> UserData { get; set; } = new ConcurrentDictionary<string, SteamUserData>();

        public int IncrementNumMembers()
        {
            return Interlocked.Increment(ref _numMembers);
        }

        public int DecrementNumMembers()
        {
            return Interlocked.Decrement(ref _numMembers);
        }

        private int _numMembers = 0;
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
        private readonly IUserSessions _sessions;
        private readonly ISteamService _steam;
        private readonly ILogger _logger;
        private readonly ISerializer _serializer;
        private readonly IServiceLocator _locator;

        /// <summary>
        /// Steam Party Event Handler contructor.
        /// </summary>
        /// <param name="sessions"></param>
        /// <param name="steam"></param>
        /// <param name="logger"></param>
        /// <param name="rpc"></param>
        /// <param name="serializer"></param>
        /// <param name="locator"></param>
        public SteamPartyEventHandler(
            IUserSessions sessions,
            ISteamService steam,
            ILogger logger,
            RpcService rpc,
            ISerializer serializer,
            IServiceLocator locator
        )
        {
            _rpc = rpc;
            _sessions = sessions;
            _steam = steam;
            _logger = logger;
            _serializer = serializer;
            _locator = locator;
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
            if (ctx.Accept && ctx.Session.platformId.Platform == SteamConstants.PROVIDER_NAME)
            {
                if (ctx.Party.Settings.ServerSettings.TryGetValue(SteamSettingsConstants.CreateLobbyPartyServerSetting, out var createLobby) && createLobby != "false")
                {
                    var data = (SteamPartyData)ctx.Party.ServerData.GetOrAdd(PartyLobbyKey, new SteamPartyData());

                    await data.TaskQueue.PushWork(async () =>
                    {
                        try
                        {
                            // Get steamId
                            ulong steamId;
                            var steamIdStr = (string?)ctx.Session.User.Auth[SteamConstants.PROVIDER_NAME]?[SteamConstants.ClaimPath];

                            if (steamIdStr == null)
                            {
                                throw new Exception("SteamId is null");
                            }

                            try
                            {
                                steamId = ulong.Parse(steamIdStr);
                            }
                            catch (Exception)
                            {
                                throw new Exception("SteamId is invalid");
                            }

                            // If Steam lobby does not exist, we create it
                            if (data.SteamIdLobby == 0)
                            {
                                var maxMembers = 5;
                                if (ctx.Party.Settings.ServerSettings.ContainsKey("platform.maxMembers"))
                                {
                                    try
                                    {
                                        maxMembers = int.Parse(ctx.Party.Settings.ServerSettings["platform.maxMembers"]);
                                    }
                                    catch (Exception)
                                    {
                                        // Do nothing (default value already set)
                                    }
                                }

                                var lobbyType = LobbyType.FriendsOnly;
                                if (ctx.Party.Settings.ServerSettings.ContainsKey("platform.lobbyType"))
                                {
                                    if (!Enum.TryParse(ctx.Party.Settings.ServerSettings["platform.lobbyType"], true, out lobbyType))
                                    {
                                        // Do nothing (default value already set)
                                    }
                                }

                                _logger.Log(LogLevel.Trace, "SteamPartyEventHandler.OnJoining", "Creating steam lobby...", new
                                {
                                    ctx.Party.Settings.PartyId,
                                    UserId = ctx.Session.User.Id,
                                    ctx.Session.SessionId
                                });

                                var lobbyName = $"{LobbyPrefix}{ctx.Party.Settings.PartyId}";
                                var result = await _steam.CreateLobby(lobbyName, LobbyType.FriendsOnly, maxMembers, new List<ulong> { steamId }, new Dictionary<string, string> { { "PartyId", ctx.Party.Settings.PartyId } });

                                if (result.appid != 0 && !string.IsNullOrEmpty(result.steamid_lobby))
                                {
                                    _logger.Log(LogLevel.Trace, "SteamPartyEventHandler.OnJoining", "Steam lobby created", new
                                    {
                                        SteamIdLobby = result.steamid_lobby,
                                        ctx.Party.Settings.PartyId,
                                        UserId = ctx.Session.User.Id,
                                        ctx.Session.SessionId
                                    });
                                    var partySettingsDto = new PartySettingsDto
                                    {
                                        CustomData = ctx.Party.Settings.CustomData,
                                        GameFinderName = ctx.Party.Settings.GameFinderName,
                                        IsJoinable = ctx.Party.Settings.IsJoinable,
                                        OnlyLeaderCanInvite = ctx.Party.Settings.OnlyLeaderCanInvite,
                                        PublicServerData = ctx.Party.Settings.PublicServerData
                                    };
                                    partySettingsDto.PublicServerData["SteamIdLobby"] = result.steamid_lobby;
                                    _ = ctx.Party.UpdateSettings(partySettingsDto);
                                    data.SteamIdLobby = ulong.Parse(result.steamid_lobby);
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

                            if (ctx.Accept)
                            {
                                await _sessions.SendRequest("Steam.JoinLobby", "", ctx.Session.User.Id, stream =>
                                {
                                    _serializer.Serialize(new SteamUserJoinLobbyDto { lobbyIdSteam = data.SteamIdLobby }, stream);
                                }, CancellationToken.None).LastOrDefaultAsync();

                                data.UserData[ctx.Session.SessionId] = new SteamUserData { SessionId = ctx.Session.SessionId, SteamId = steamId };

                                data.IncrementNumMembers();
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

        private async Task RemoveUserFromLobby(ConcurrentDictionary<string, object> serverData, string sessionId)
        {
            if (serverData.TryGetValue(PartyLobbyKey, out var dataObject))
            {
                var data = (SteamPartyData)dataObject;
                if (data != null && data.SteamIdLobby != 0 && data.UserData.TryGetValue(sessionId, out var steamUserData))
                {
                    await data.TaskQueue.PushWork(async () =>
                    {
                        await _steam.RemoveUserFromLobby(steamUserData.SteamId, data.SteamIdLobby);
                        data.DecrementNumMembers();
                        data.UserData.TryRemove(sessionId, out var _);
                    });
                }
            }
        }
    }
}
