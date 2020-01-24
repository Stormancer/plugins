using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Plugins.ServiceLocator;
using Stormancer.Plugins.Utils;
using Stormancer.Server.Party;
using Stormancer.Server.Party.Dto;
using Stormancer.Server.Plugins.Steam.Models;
using Stormancer.Server.Users;
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
        public string SessionId { get; set; }

        public ulong SteamId { get; set; } = 0;
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

    [Priority(int.MinValue)]
    public class SteamPartyEventHandler : IPartyEventHandler
    {
        public static string LobbyPrefix = "Party-";
        private const string PartyLobbyKey = "steam.lobby";

        private readonly RpcService _rpc;
        private readonly IUserSessions _sessions;
        private readonly ISteamService _steam;
        private readonly ILogger _logger;
        private readonly ISerializer _serializer;
        private readonly IServiceLocator _locator;

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

        public Task OnCreatingParty(PartyCreationContext ctx)
        {
            return Task.CompletedTask;
        }

        public async Task OnJoinDenied(JoinDeniedContext ctx)
        {
            await RemoveUserFromLobby(ctx.Party.ServerData, ctx.Session.SessionId);
        }

        public Task OnJoined(JoinedPartyContext ctx)
        {
            return Task.CompletedTask;
        }

        public async Task OnJoining(JoiningPartyContext ctx)
        {
            if (ctx.Accept && ctx.Session.platformId.Platform == SteamAuthenticationProvider.PROVIDER_NAME)
            {
                var data = (SteamPartyData)ctx.Party.ServerData.GetOrAdd(PartyLobbyKey, new SteamPartyData());

                await data.TaskQueue.PushWork(async () =>
                {
                    try
                    {
                        // Get steamId
                        ulong steamId;
                        var steamIdStr = (string)ctx.Session.User.Auth[SteamAuthenticationProvider.PROVIDER_NAME]?[SteamAuthenticationProvider.ClaimPath];
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
                                try
                                {
                                    switch (ctx.Party.Settings.ServerSettings["platform.lobbyType"])
                                    {
                                        case "public":
                                            lobbyType = LobbyType.Public;
                                            break;
                                        case "private":
                                            lobbyType = LobbyType.Private;
                                            break;
                                        case "privateUnique":
                                            lobbyType = LobbyType.PrivateUnique;
                                            break;
                                        case "friendsOnly":
                                            lobbyType = LobbyType.FriendsOnly;
                                            break;
                                        case "invisible":
                                            lobbyType = LobbyType.Invisible;
                                            break;
                                    }
                                }
                                catch (Exception)
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
                            await _sessions.SendRequest("Steam.JoinLobby", null, ctx.Session.User.Id, stream =>
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

        public Task OnPlayerReadyStateChanged(PlayerReadyStateContext ctx)
        {
            return Task.CompletedTask;
        }

        public async Task OnQuit(QuitPartyContext ctx)
        {
            await RemoveUserFromLobby(ctx.Party.ServerData, ctx.Args.Peer.SessionId);
        }

        public Task OnUpdateSettings(PartySettingsUpdateCtx ctx)
        {
            return Task.CompletedTask;
        }

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
