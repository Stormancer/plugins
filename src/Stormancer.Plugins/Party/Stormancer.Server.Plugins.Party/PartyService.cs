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


using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameFinder;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.Party.Interfaces;
using Stormancer.Server.Plugins.Party.Model;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    class PartyService : IPartyService
    {
        // stormancer.party => <protocol version>
        // stormancer.party.revision => <PartyService revision>
        // Revision is independent from protocol version. Revision changes when a modification is made to server code (e.g bugfix).
        // Protocol version changes when a change to the communication protocol is made.
        // Protocol versions between client and server are not obligated to match.
        public const string REVISION = "2020-08-21.1";
        public const string REVISION_METADATA_KEY = "stormancer.party.revision";
        private const string LOG_CATEGORY = "PartyService";

        //Dependencies
        private readonly ISceneHost _scene;
        private readonly ILogger _logger;
        private readonly IUserSessions _userSessions;
        private readonly GameFinderProxy _gameFinderClient;
        private readonly IServiceLocator _locator;
        private readonly Func<IEnumerable<IPartyEventHandler>> _handlers;
        private readonly PartyState _partyState;
        private readonly RpcService _rpcService;
        private readonly IConfiguration configuration;
        private readonly IUserService _users;
        private readonly IEnumerable<IPartyPlatformSupport> _platformSupports;
        private readonly InvitationCodeService invitationCodes;
        private readonly PartyLuceneDocumentStore partyDocumentsStore;
        private readonly PartyConfigurationService partyConfigurationService;
        private readonly IProfileService _profiles;
        private readonly PartyAnalyticsWorker _analyticsWorker;
        private readonly ISerializer _serializer;
        private readonly CrossplayService _crossplayService;
        private readonly IClusterSerializer _clusterSerializer;

        public IReadOnlyDictionary<SessionId, PartyMember> PartyMembers => _partyState.PartyMembers;

        public PartyConfiguration Settings => _partyState.Settings;
        public PartyState State => _partyState;
        private TimeSpan _clientRpcTimeout = TimeSpan.FromSeconds(2);

        public PartyService(
            ISceneHost scene,
            ILogger logger,
            IUserSessions userSessions,
            GameFinderProxy gameFinderClient,
            IServiceLocator locator,
            Func<IEnumerable<IPartyEventHandler>> handlers,
            PartyState partyState,
            RpcService rpcService,
            IConfiguration configuration,
            IUserService users,
            IEnumerable<IPartyPlatformSupport> platformSupports,
            InvitationCodeService invitationCodes,
            PartyLuceneDocumentStore partyDocumentsStore,
            PartyConfigurationService partyConfigurationService,
            IProfileService profiles,
            PartyAnalyticsWorker analyticsWorker,
            ISerializer serializer,
            CrossplayService crossplayService,
            IClusterSerializer clusterSerializer
        )
        {
            _handlers = handlers;
            _scene = scene;
            _logger = logger;
            _userSessions = userSessions;
            _gameFinderClient = gameFinderClient;
            _locator = locator;
            _partyState = partyState;
            _rpcService = rpcService;
            this.configuration = configuration;
            _users = users;
            _platformSupports = platformSupports;
            this.invitationCodes = invitationCodes;
            this.partyDocumentsStore = partyDocumentsStore;
            this.partyConfigurationService = partyConfigurationService;
            _profiles = profiles;
            _analyticsWorker = analyticsWorker;
            _serializer = serializer;
            this._crossplayService = crossplayService;
            _clusterSerializer = clusterSerializer;
            ApplySettings(configuration.Settings);



        }

        private const string JoinDeniedError = "party.joinDenied";
        private const string GameFinderNameError = "party.badArgument.GameFinderName";
        private const string CannotKickLeaderError = "party.cannotKickLeader";
        private const string SettingsOutdatedError = "party.settingsOutdated";
        private const string GenericJoinError = "party.joinError";

        private const string LeaderChangedRoute = "party.leaderChanged";
        private const string MemberConnectedRoute = "party.memberConnected";
        private const string SendPartyStateRoute = "party.getPartyStateResponse";
        private const string GameFinderFailedRoute = "party.gameFinderFailed";


        private async Task RemoveExpiredReservations(TimeSpan delay, SessionId reservationSessionId)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (!_partyState.IsDisposed)
                {
                    foreach (var (sessionId, expiredReservation) in _partyState.PartyMembers.Where(p => p.Value.ConnectionStatus == PartyMemberConnectionStatus.Reservation && p.Value.CreatedOnUtc + TimeSpan.FromSeconds(10) < DateTime.UtcNow).ToArray())
                    {

                        if (_partyState.PartyMembers.TryGetValue(sessionId, out var member) && member.ConnectionStatus == PartyMemberConnectionStatus.Reservation && _partyState.PartyMembers.TryRemove(sessionId, out _))
                        {


                            var handlers = _handlers();
                            var partyResetCtx = new PartyMemberReadyStateResetContext(PartyMemberReadyStateResetEventType.PartyMembersListUpdated, _scene);
                            partyConfigurationService.ShouldResetPartyMembersReadyState(partyResetCtx);
                            await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(partyResetCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while processing an 'OnPlayerReadyStateRest' event.", ex));

                            if (partyResetCtx.ShouldReset)
                            {
                                await TryCancelPendingGameFinder();
                            }
                            await BroadcastStateUpdateRpc(PartyMemberDisconnection.Route, new PartyMemberDisconnection { UserId = member.UserId, Reason = PartyDisconnectionReason.Left });
                        }
                    }
                }
            });
        }

        private void ApplySettings(dynamic settings)
        {
            var timeoutSetting = settings?.party?.clientAckTimeoutSeconds;
            var timeout = (double?)timeoutSetting;

            if (timeout.HasValue && timeout.Value > 0)
            {
                _clientRpcTimeout = TimeSpan.FromSeconds(timeout.Value);
            }
            else if (timeoutSetting != null)
            {
                _logger.Warn("PartyService.ApplySettings", "party.clientAckTimeoutSeconds must be a strictly positive decimal number");
            }
        }

        [DoesNotReturn]
        private static void ThrowNoSuchMemberError(SessionId sessionId) => throw new ClientException($"party.noSuchMember?sessionId={sessionId}");

        [DoesNotReturn]
        private static void ThrowNoSuchMemberError(string userId) => throw new ClientException($"party.noSuchMember?userId={userId}");

        [DoesNotReturn]
        private static void ThrowNoSuchUserError(string userId) => throw new ClientException($"party.noSuchUser?userId={userId}");

        private bool TryGetMemberByUserId(string userId,[NotNullWhen(true)] out PartyMember? member)
        {
            member = _partyState.PartyMembers.FirstOrDefault(kvp => kvp.Value.UserId == userId).Value;
            return member != null;
        }
        private bool TryGetMemberBySessionId(SessionId sessionId,[NotNullWhen(true)] out PartyMember? member)
        {
            return _partyState.PartyMembers.TryGetValue(sessionId, out member);
        }

        private void Log(LogLevel level, string methodName, string message, string sessionId, string? userId = null)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Log(level, $"PartyService.{methodName}", message,
                    new { PartyId = _partyState.Settings.PartyId, SessionId = sessionId },
                    _partyState.Settings.PartyId, sessionId);
            }
            else
            {
                _logger.Log(level, $"PartyService.{methodName}", message, new { _partyState.Settings.PartyId, SessionId = sessionId, UserId = userId },
                    _partyState.Settings.PartyId, sessionId, userId);
            }
        }

        private void Log(LogLevel level, string methodName, string message, object? data = null, params string[] tags)
        {
            var totalParams = tags?.Append(_partyState.Settings.PartyId)?.ToArray() ?? new string[] { _partyState.Settings.PartyId };

            _logger.Log(level, $"PartyService.{methodName}", message,
                new { PartyId = _partyState.Settings.PartyId, Data = data },
                totalParams);
        }

        internal async Task OnConnecting(IScenePeerClient peer)
        {
            var handlers = _handlers();
            await _partyState.TaskQueue.PushWork(async () =>
            {


                if (!_partyState.PartyMembers.ContainsKey(peer.SessionId))
                {
                    if (_partyState.MemberCount >= _partyState.Settings.ServerSettings.MaxMembers())
                    {
                        Log(LogLevel.Trace, "OnConnecting", "Party join denied because the party is full.", peer.SessionId);
                        throw new ClientException(JoinDeniedError + "?reason=partyFull");
                    }

                    if (!_partyState.Settings.IsJoinable)
                    {
                        Log(LogLevel.Trace, "OnConnecting", "Party join denied because the party is not joinable.", peer.SessionId);
                        throw new ClientException(JoinDeniedError + "?reason=notJoinable");
                    }
                }

                var session = await _userSessions.GetSession(peer, CancellationToken.None);
                if (session?.User == null)
                {
                    throw new ClientException(JoinDeniedError + "?reason=notAuthenticated");
                }

                CheckCrossPlay(session.User);


                var userData = peer.ContentType == "party/userdata" ? peer.UserData : new byte[0];
                var ctx = new JoiningPartyContext(this, session, peer, _partyState.PendingAcceptedPeers.Count + _partyState.PartyMembers.Count, userData);
                await handlers.RunEventHandler(h => h.OnJoining(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running OnJoining", ex));
                if (!ctx.Accept)
                {
                    Log(LogLevel.Trace, "OnConnecting", "Join denied by event handler", peer.SessionId, session.User.Id);
                    var message = JoinDeniedError;
                    if (!string.IsNullOrWhiteSpace(ctx.Reason))
                    {
                        message += $"?reason={ctx.Reason}";
                    }

                    _partyState.PartyMembers.TryRemove(peer.SessionId, out _);

                    throw new ClientException(message);
                }

                Log(LogLevel.Trace, "OnConnecting", "Join accepted", peer.SessionId, session.User.Id);
                _partyState.PendingAcceptedPeers.Add(peer);
            });
        }

        private bool IsCrossPlayEnabled()
        {

            return _partyState.Platform == null;

        }
        private void CheckCrossPlay(User user)
        {
            var crossplayEnabled = _crossplayService.IsCrossplayEnabled(user);
           
            //If cross play is disabled on the player, and we are the first player, set the platform of the party.
            if (_partyState.PartyMembers.Count == 0)
            {
                if (!crossplayEnabled)
                {
                    _partyState.Platform = user.LastPlatform;

                }

                return;
            }

            if (crossplayEnabled != IsCrossPlayEnabled())
            {
                throw new ClientException(JoinDeniedError + "?reason=crossPlay.MismatchedOption");
            }

            if (_partyState.Platform != null && user.LastPlatform != _partyState.Platform)
            {
                throw new ClientException(JoinDeniedError + "?reason=crossPlay.MismatchedPlatform&partyPlatform=" + _partyState.Platform);
            }

        }

        internal async Task OnConnectionRejected(IScenePeerClient peer)
        {
            await _partyState.TaskQueue.PushWork(() =>
            {
                Log(LogLevel.Trace, "OnConnectionRejected", "Connection to party was rejected", peer.SessionId);
                _partyState.PendingAcceptedPeers.Remove(peer);
                _ = RunInRequestScope(async (service, handlers, scope) =>
                {
                    var session = await service._userSessions.GetSession(peer, CancellationToken.None);
                    if (session != null)
                    {
                        var deniedCtx = new JoinDeniedContext(service, session);
                        await handlers.RunEventHandler(handler => handler.OnJoinDenied(deniedCtx), exception =>
                        {
                            service.Log(LogLevel.Error, "OnConnectionRejected", "An exception was thrown by an OnJoinDenied event handler", new { exception }, peer.SessionId.ToString());
                        });
                    }
                });
                return Task.CompletedTask;
            });
        }

        internal async Task OnConnected(IScenePeerClient peer)
        {
            await _partyState.TaskQueue.PushWork(async () =>
            {
                _partyState.PendingAcceptedPeers.Remove(peer);
                var userData = peer.ContentType == "party/userdata" ? peer.UserData : new byte[0];
                var session = await _userSessions.GetSession(peer, CancellationToken.None);


                if (session == null)
                {
                    await peer.Disconnect(GenericJoinError);
                    return;
                }
                var user = session.User;

                if (user == null)
                {
                    await peer.Disconnect("noUser");
                    return;
                }

                string? errorId = null;
                await RunInRequestScope(async (service, handlers, scope) =>
                {
                    var joinedCtx = new PreJoinedPartyContext(service, peer, session, userData);
                    await handlers.RunEventHandler(handler => handler.OnPreJoined(joinedCtx), exception =>
                    {
                        service.Log(LogLevel.Error, "OnConnected", "An exception was thrown by an OnJoined event handler", new { exception }, peer.SessionId.ToString(), user.Id);
                    });
                    errorId = joinedCtx.ErrorId;
                });


                if (errorId != null)
                {
                    await Task.Delay(1000);
                    await peer.Disconnect(GenericJoinError + "?reason=" + errorId);
                    return;
                }




                await _userSessions.UpdateSessionData(peer.SessionId, "party", _partyState.Settings.PartyId, CancellationToken.None);


                if (_partyState.PartyMembers.TryGetValue(peer.SessionId, out var partyUser))
                {
                    partyUser.ConnectionStatus = PartyMemberConnectionStatus.Connected;
                    partyUser.Peer = peer;
                    await BroadcastStateUpdateRpc(PartyMemberDataUpdate.Route, new PartyMemberDataUpdate { UserId = partyUser.UserId, UserData = partyUser.UserData, LocalPlayers = partyUser.LocalPlayers, ConnectionStatus = partyUser.ConnectionStatus });
                }
                else
                {

                    var profile = await _profiles.GetProfile(user.Id, new Dictionary<string, string> { ["user"] = "summary" }, session, CancellationToken.None);

                    var mainLocalUser = new Models.LocalPlayerInfos { StormancerUserId = user.Id, Platform = session.platformId.Platform, PlatformId = session.platformId.PlatformUserId, Pseudo = profile?["user"]?["pseudo"]?.ToObject<string>() ?? "anonymous" };

                    partyUser = new PartyMember { UserId = user.Id, SessionId = peer.SessionId, StatusInParty = PartyMemberStatus.NotReady, Peer = peer, UserData = userData, LocalPlayers = new List<Models.LocalPlayerInfos> { mainLocalUser }, ConnectionStatus = PartyMemberConnectionStatus.Connected };
                    _partyState.PartyMembers.TryAdd(peer.SessionId, partyUser);

                    await BroadcastStateUpdateRpc(MemberConnectedRoute, new PartyMemberDto { PartyUserStatus = partyUser.StatusInParty, UserData = partyUser.UserData, UserId = partyUser.UserId, SessionId = partyUser.Peer.SessionId, LocalPlayers = partyUser.LocalPlayers, ConnectionStatus = PartyMemberConnectionStatus.Connected });

                }
                // Complete existing invitations for the new user. These invitations should all have been completed by now, but this is hard to guarantee.
                if (_partyState.PendingInvitations.TryGetValue(user.Id, out var invitations))
                {
                    foreach (var invitation in invitations)
                    {
                        invitation.Value.TaskCompletionSource.TrySetResult(true);
                    }
                    _partyState.PendingInvitations.Remove(user.Id);


                }



                await RunInRequestScope((service, handlers, scope) =>
                {
                    var joinedCtx = new JoinedPartyContext(service, peer, session, partyUser.UserData);
                    return handlers.RunEventHandler(handler => handler.OnJoined(joinedCtx), exception =>
                    {
                        service.Log(LogLevel.Error, "OnConnected", "An exception was thrown by an OnJoined event handler", new { exception }, peer.SessionId.ToString(), user.Id);
                    });
                });
            });
        }

        private async Task RunInRequestScope(Func<PartyService, IEnumerable<IPartyEventHandler>, IDependencyResolver, Task> runner)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                // Resolve the service on the new scope to avoid scope errors in event handlers
                var service = (PartyService)scope.Resolve<IPartyService>();
                var handlers = scope.ResolveAll<IPartyEventHandler>();
                await runner(service, handlers, scope);
            }
        }

        private PartyDisconnectionReason ParseDisconnectionReason(string reason)
        {
            if (reason == "party.kicked")
            {
                return PartyDisconnectionReason.Kicked;
            }
            return PartyDisconnectionReason.Left;
        }

        internal async Task OnDisconnected(DisconnectedArgs args)
        {
            await _partyState.TaskQueue.PushWork(async () =>
            {
                try
                {

                    if (_partyState.PartyMembers.TryRemove(args.Peer.SessionId, out var partyUser))
                    {
                        Log(LogLevel.Trace, "OnDisconnected", $"Member left the party, reason: {args.Reason}", args.Peer.SessionId, partyUser.UserId);

                        var handlers = _handlers();
                        var partyResetCtx = new PartyMemberReadyStateResetContext(PartyMemberReadyStateResetEventType.PartyMembersListUpdated, _scene);
                        partyConfigurationService.ShouldResetPartyMembersReadyState(partyResetCtx);
                        await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(partyResetCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while processing an 'OnPlayerReadyStateRest' event.", ex));

                        if (partyResetCtx.ShouldReset)
                        {
                            await TryCancelPendingGameFinder();
                        }

                        if (_partyState.Settings.PartyLeaderId == partyUser.UserId && _partyState.PartyMembers.Count != 0)
                        {
                            // Change party leader
                            _partyState.Settings.PartyLeaderId = _partyState.PartyMembers.FirstOrDefault().Value.UserId;
                            Log(LogLevel.Trace, "OnDisconnected", $"New leader elected: {_partyState.Settings.PartyLeaderId}", args.Peer.SessionId, partyUser.UserId);
                            await BroadcastStateUpdateRpc(LeaderChangedRoute, _partyState.Settings.PartyLeaderId);
                        }


                        await _userSessions.UpdateSessionData(args.Peer.SessionId, "party", Array.Empty<byte>(), CancellationToken.None);
                        await BroadcastStateUpdateRpc(PartyMemberDisconnection.Route, new PartyMemberDisconnection { UserId = partyUser.UserId, Reason = ParseDisconnectionReason(args.Reason) });


                    }
                }
                finally
                {
                   
                }

                _ = RunInRequestScope(async (service, handlers, scope) =>
                {
                    try
                    {
                        var ctx = new QuitPartyContext(service, args);
                        await handlers.RunEventHandler(handler => handler.OnQuit(ctx), exception => service.Log(LogLevel.Error, "OnDisconnected", "An exception was thrown by an OnQuit event handler", new { exception }, args.Peer.SessionId.ToString()));
                    }
                    finally
                    {
                        if (_partyState.PartyMembers.IsEmpty)
                        {
                            partyDocumentsStore.DeleteDocument(_partyState.Settings.PartyId);
                            CancelInvitationCode();
                            _ = _scene.KeepAlive(TimeSpan.Zero);
                        }
                    }
                });
            });
        }

        public void SetConfiguration(Dictionary<string, object?> metadata)
        {
            if (metadata.TryGetValue("party", out var content) && content != null)
            {

                _partyState.Settings = JObject.FromObject(content).ToObject<PartyConfiguration>()!;
                _partyState.VersionNumber = 1;
            }
            else
            {
                throw new InvalidOperationException("Scene metadata has no 'party' field");
            }
        }
        public Task UpdateSettings(Func<PartyState, PartySettingsDto?> partySettingsUpdater, CancellationToken ct)
        {
            return _partyState.TaskQueue.PushWork(async () =>
            {

                try
                {
                    var partySettingsDto = partySettingsUpdater(_partyState);


                    if (partySettingsDto == null)
                    {
                        return;
                    }
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    if (partySettingsDto.GameFinderName == "")
                    {
                        throw new ClientException(GameFinderNameError);
                    }



                    await using var scope = _scene.CreateRequestScope();
                    var handlers = scope.Resolve<IEnumerable<IPartyEventHandler>>();

                    var originalDto = partySettingsDto.Clone();
                    var ctx = new PartySettingsUpdateCtx(this, partySettingsDto);

                    await handlers.RunEventHandler(h => h.OnUpdatingSettings(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running OnUpdatingSettings", ex));

                    if (!ctx.ApplyChanges)
                    {
                        Log(LogLevel.Trace, "UpdateSettings", "Settings update refused by event handler", partySettingsDto);
                        throw new ClientException(ctx.ErrorMsg);
                    }


                    await handlers.RunEventHandler(h => h.OnUpdateSettings(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running OnUpdateSettings", ex));

                    // If the event handlers have modified the settings, we need to notify the leader to invalidate their local copy.
                    // Make an additional bump to the version number to achieve this.
                    int newSettingsVersion = _partyState.SettingsVersionNumber + 1;
                    if (!partySettingsDto.Equals(originalDto))
                    {
                        newSettingsVersion = _partyState.SettingsVersionNumber + 2;
                    }

                    var changed = false;

                    if (_partyState.Settings.GameFinderName != partySettingsDto.GameFinderName)
                    {
                        changed = true;
                        _partyState.Settings.GameFinderName = partySettingsDto.GameFinderName;
                    }

                    if (_partyState.Settings.CustomData != partySettingsDto.CustomData)
                    {
                        changed = true;
                        _partyState.Settings.CustomData = partySettingsDto.CustomData;
                    }

                    if (_partyState.Settings.OnlyLeaderCanInvite != partySettingsDto.OnlyLeaderCanInvite)
                    {
                        changed = true;
                        _partyState.Settings.OnlyLeaderCanInvite = partySettingsDto.OnlyLeaderCanInvite;
                    }

                    if (_partyState.Settings.IsJoinable != partySettingsDto.IsJoinable)
                    {
                        changed = true;
                        _partyState.Settings.IsJoinable = partySettingsDto.IsJoinable;
                    }


                    if (!string.IsNullOrEmpty(partySettingsDto.IndexedDocument))
                    {
                        try
                        {
                            var json = JObject.Parse(partySettingsDto.IndexedDocument);

                            if (_partyState.SearchDocument == null || !JToken.DeepEquals(json, _partyState.SearchDocument))
                            {
                                changed = true;
                                _partyState.SearchDocument = json;
                            }
                        }
                        catch (Exception)
                        {
                            if (_partyState.SearchDocument != null)
                            {
                                changed = true;
                                _partyState.SearchDocument = null;
                            }
                            //Ignore parse errors.
                        }
                    }
                    else
                    {
                        _partyState.SearchDocument = null;
                    }

                    if (partySettingsDto.PublicServerData != null)
                    {
                        //By default, changing the public server data shouldn't trigger a Ready reset, so we don't set changed to true.
                        _partyState.Settings.PublicServerData = partySettingsDto.PublicServerData;
                    }
                    _partyState.SettingsVersionNumber = newSettingsVersion;
                    //Log(LogLevel.Info, "UpdateSettings", $"Updated public server data to party='{this.PartyId}' Version={newSettingsVersion}", partySettingsDto.PublicServerData);

                    if (changed)
                    {
                        var partyResetCtx = new PartyMemberReadyStateResetContext(PartyMemberReadyStateResetEventType.PartySettingsUpdated, _scene);


                        partyConfigurationService.ShouldResetPartyMembersReadyState(partyResetCtx);
                        await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(partyResetCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while processing an 'OnPlayerReadyStateRest' event.", ex));

                        if (partyResetCtx.ShouldReset)
                        {
                            await TryCancelPendingGameFinder();
                        }
                    }
                    var msg = new PartySettingsUpdateDto(_partyState);



                    Dictionary<PartyMember, PartySettingsUpdateDto> updates = _partyState.PartyMembers.Values.ToDictionary(m => m, _ => msg);

                    await handlers.RunEventHandler(h => h.OnSendingSettingsUpdateToMembers(new PartySettingsMemberUpdateCtx(this, updates)),
                    ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running OnSendingSettingsToMember", ex));
                    //Log(LogLevel.Info, "UpdateSettings", $"Sending settings update to party='{this.PartyId}' Version={newSettingsVersion}", msg);
                    await BroadcastStateUpdateRpc(PartySettingsUpdateDto.Route, updates);
                }
                finally
                {
                    partyDocumentsStore.UpdateDocument(_partyState.Settings.PartyId, _partyState.SearchDocument, _partyState.Settings.CustomData);
                }
            });
        }

        public Task UpdateSettings(PartySettingsDto partySettingsDto, CancellationToken ct)
        {
            return UpdateSettings(state =>
                new PartySettingsDto(state)
                {
                    CustomData = partySettingsDto.CustomData,
                    GameFinderName = partySettingsDto.GameFinderName,
                    IndexedDocument = partySettingsDto.IndexedDocument,
                    IsJoinable = partySettingsDto.IsJoinable,
                    OnlyLeaderCanInvite = partySettingsDto.OnlyLeaderCanInvite,
                }
             , ct);

        }

        /// <summary>
        /// Player status 
        /// </summary>
        /// <param name="partyUserStatus"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task UpdateGameFinderPlayerStatus(string userId, PartyMemberStatusUpdateRequest partyUserStatus, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("message", nameof(userId));
            }

            if (partyUserStatus is null)
            {
                throw new ArgumentNullException(nameof(partyUserStatus));
            }

            var handlers = _handlers();
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!TryGetMemberByUserId(userId, out var user))
                {
                    ThrowNoSuchMemberError(userId);
                }

                if (user.StatusInParty == partyUserStatus.DesiredStatus)
                {
                    return;
                }
                // Prevent the member from setting themselves to ready if they have outdated party settings
                if (partyUserStatus.DesiredStatus == PartyMemberStatus.Ready && partyUserStatus.ClientSettingsVersion < _partyState.SettingsVersionNumber)
                {
                    throw new ClientException(SettingsOutdatedError);
                }

                var updatingCtx = new UpdatingPlayerReadyStateContext(this, user, _scene, partyUserStatus.DesiredStatus);

                await handlers.RunEventHandler(h => h.OnUpdatingPlayerReadyState(updatingCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running 'OnUpdatingPlayerReadyState'", ex));

                if (!updatingCtx.Accept)
                {
                    throw new ClientException(string.IsNullOrEmpty(updatingCtx.ErrorId) ? "party.ready.updateDenied" : updatingCtx.ErrorId);
                }

                user.StatusInParty = partyUserStatus.DesiredStatus;
                Log(LogLevel.Trace, "UpdateGameFinderPlayerStatus", $"Updated user status, new value: {partyUserStatus}", user.SessionId, user.UserId);

                var update = new BatchStatusUpdate();
                update.UserStatus.Add(new PartyMemberStatusUpdate { UserId = userId, Status = user.StatusInParty });
                await BroadcastStateUpdateRpc(BatchStatusUpdate.Route, update);

                var eventHandlerCtx = new PlayerReadyStateContext(this, user, _scene);
                await handlers.RunEventHandler(h => h.OnPlayerReadyStateChanged(eventHandlerCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running OnPlayerReadyStateChanged", ex));


                bool shouldLaunchGameFinderRequest = false;
                switch (eventHandlerCtx.GameFinderPolicy)
                {
                    case GameFinderRequestPolicy.StartNow:
                        shouldLaunchGameFinderRequest = true;
                        break;
                    case GameFinderRequestPolicy.StartWhenAllMembersReady:
                        shouldLaunchGameFinderRequest = _partyState.PartyMembers.All(kvp => kvp.Value.StatusInParty == PartyMemberStatus.Ready);
                        break;
                    case GameFinderRequestPolicy.DoNotStart:
                        shouldLaunchGameFinderRequest = false;
                        break;
                }
                _partyState.LastGameFinderRequestPolicy = eventHandlerCtx.GameFinderPolicy;
                var oldState = GameFinderState;
                _partyState.GameFinderLaunchPending = shouldLaunchGameFinderRequest;
                var newState = GameFinderState;

                if (oldState != newState)
                {
                    await RaiseGameFinderStateChanged(oldState, newState);
                }


                if (shouldLaunchGameFinderRequest)
                {
                    _ = _partyState.TaskQueue.PushWork(async () =>
                    {
                        if (_partyState.GameFinderLaunchPending)
                        {
                            var oldState = this.GameFinderState;
                            _partyState.GameFinderLaunchPending = false;



                            bool shouldLaunchGameFinderRequest = false;

                            switch (_partyState.LastGameFinderRequestPolicy)
                            {
                                case GameFinderRequestPolicy.StartNow:
                                    shouldLaunchGameFinderRequest = true;
                                    break;
                                case GameFinderRequestPolicy.StartWhenAllMembersReady:
                                    shouldLaunchGameFinderRequest = _partyState.PartyMembers.All(kvp => kvp.Value.StatusInParty == PartyMemberStatus.Ready);
                                    break;
                                case GameFinderRequestPolicy.DoNotStart:
                                    shouldLaunchGameFinderRequest = false;
                                    break;
                            }


                            if (shouldLaunchGameFinderRequest)
                            {
                                Log(LogLevel.Trace, "UpdateGameFinderPlayerStatus", "Launching a FindGame request");
                                LaunchGameFinderRequest();
                            }
                            else if (IsGameFinderRunning)
                            {
                                await TryCancelPendingGameFinder();
                            }

                            var newState = GameFinderState;

                            if (oldState != newState)
                            {
                                await RaiseGameFinderStateChanged(oldState, newState);
                            }
                        }
                    });
                }
                else if (IsGameFinderRunning)
                {
                    await TryCancelPendingGameFinder();
                }

            });
        }

        private PartyGameFinderStateChange GameFinderState
        {
            get
            {
                if (_partyState.GameFinderLaunchPending && !IsGameFinderRunning)
                {
                    return PartyGameFinderStateChange.StartPending;
                }
                else if (IsGameFinderRunning)
                {
                    return PartyGameFinderStateChange.Started;
                }
                else
                {
                    return PartyGameFinderStateChange.Stopped;
                }
            }
        }
        private async Task RaiseGameFinderStateChanged(PartyGameFinderStateChange old, PartyGameFinderStateChange newState)
        {
            await using var scope = _scene.CreateRequestScope();
            var ctx = new GameFinderStateChangedContext(this, _scene, old, newState);
            await scope.ResolveAll<IPartyEventHandler>().RunEventHandler(h => h.OnGameFinderStateChanged(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running {}.", ex));
        }
        public bool IsGameFinderRunning
        {
            get
            {
                return _partyState.FindGameRequest != null;
            }
        }

        public ConcurrentDictionary<string, object> ServerData => _partyState.ServerData;

        public string PartyId => Settings.PartyId;



        public async Task UpdatePartyUserData(string userId, byte[] data, List<Models.LocalPlayerInfos> localPlayers, CancellationToken ct)
        {
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (!TryGetMemberByUserId(userId, out var partyUser))
                {
                    ThrowNoSuchMemberError(userId);
                }



                var localPlayerCountChanged = !localPlayers.SequenceEqual(partyUser.LocalPlayers);
                var userDataChanged = !partyUser.UserData.SequenceEqual(data);

                if (localPlayerCountChanged && _partyState.MemberCount - partyUser.LocalPlayers.Count + localPlayers.Count > _partyState.Settings.ServerSettings.MaxMembers())
                {
                    throw new ClientException(JoinDeniedError + "?reason=partyFull");
                }



                await using var scope = _scene.CreateRequestScope();
                var handlers = scope.Resolve<IEnumerable<IPartyEventHandler>>();

                var ctx = new UpdatingPartyMemberDataContext(partyUser, localPlayers, data, _scene, this);
                await handlers.RunEventHandler(h => h.OnUpdatingPartyMemberData(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running the event 'OnUpdatingPartyMemberData'.", ex));

                if (!ctx.IsUpdateValid)
                {
                    throw new ClientException(ctx.Error ?? "party.invalidMemberData");
                }



                partyUser.LocalPlayers = localPlayers;
                partyUser.UserData = data;
                Log(LogLevel.Trace, "UpdatePartyUserData", "Updated user data", new { partyUser.SessionId, partyUser.UserId, UserData = data });

                var flags = (localPlayerCountChanged ? PartyMemberReadyStateResetEventType.PartyMembersListUpdated : 0) | (userDataChanged ? PartyMemberReadyStateResetEventType.PartyMemberDataUpdated : 0);

                if (flags != 0)
                {
                    var partyResetCtx = new PartyMemberReadyStateResetContext(flags, _scene);
                    partyConfigurationService.ShouldResetPartyMembersReadyState(partyResetCtx);
                    await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(partyResetCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while processing an 'OnPlayerReadyStateRest' event.", ex));

                    if (partyResetCtx.ShouldReset)
                    {
                        await TryCancelPendingGameFinder();
                    }

                    await BroadcastStateUpdateRpc(PartyMemberDataUpdate.Route, new PartyMemberDataUpdate { UserId = userId, UserData = partyUser.UserData, LocalPlayers = partyUser.LocalPlayers, ConnectionStatus = partyUser.ConnectionStatus });
                }
            });
        }

        public async Task PromoteLeader(string playerToPromote, CancellationToken ct)
        {
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                PartyMember user;
                if (!TryGetMemberByUserId(playerToPromote, out user))
                {
                    ThrowNoSuchMemberError(playerToPromote);
                }

                if (_partyState.Settings.PartyLeaderId == playerToPromote)
                {
                    return;
                }

                _partyState.Settings.PartyLeaderId = playerToPromote;
                Log(LogLevel.Trace, "PromoteLeader", $"Promoted new leader, userId: {user.UserId}", user.SessionId, user.UserId);

                await BroadcastStateUpdateRpc(LeaderChangedRoute, _partyState.Settings.PartyLeaderId);
            });
        }

        public async Task KickPlayer(string playerToKick, bool allowKickLeader, string? reason = null, CancellationToken ct = default)
        {
            await _partyState.TaskQueue.PushWork(async () =>
            {
                await using var scope = _scene.CreateRequestScope();

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (TryGetMemberByUserId(playerToKick, out var partyUser))
                {
                    if (playerToKick == _partyState.Settings.PartyLeaderId && !allowKickLeader)
                    {
                        throw new ClientException(CannotKickLeaderError);
                    }


                    Log(LogLevel.Trace, "KickPlayerByLeader", $"Kicked a player, userId: {partyUser.UserId}", partyUser.SessionId, partyUser.UserId);

                    if (partyUser.Peer != null)
                    {
                        await partyUser.Peer.Disconnect("party.kicked" + (reason != null ? $"?reason={reason}" : ""));
                    }
                }
                // Do not return an error if the player is already gone
            });
        }

        private void LaunchGameFinderRequest()
        {
            if (!IsGameFinderRunning)
            {

                _partyState.GameFinderLaunchPending = false;




                _partyState.FindGameCts = new CancellationTokenSource();
                _partyState.FindGameRequest = FindGame_Impl();
            }
        }

        private async Task FindGame_Impl()
        {

            //Construct gameFinder request
            var gameFinderRequest = new Models.Party() { Players = new Dictionary<string, Models.Player>() };
            gameFinderRequest.CustomData = _partyState.Settings.CustomData;
            gameFinderRequest.PartyId = _partyState.Settings.PartyId;
            gameFinderRequest.PartyLeaderId = _partyState.Settings.PartyLeaderId;
            foreach (var partyUser in _partyState.PartyMembers.Values)
            {
                gameFinderRequest.Players.Add(partyUser.UserId, new Models.Player(partyUser.SessionId, partyUser.UserId, partyUser.UserData));

            }

            gameFinderRequest.CustomData = _partyState.Settings.CustomData;

            //Send S2S find match request
            try
            {
                await using var scope = _scene.CreateRequestScope();

                var gameFinder = scope.Resolve<GameFinderProxy>();

                var findGameResult = await gameFinder.FindGame(_partyState.Settings.GameFinderName, gameFinderRequest, _partyState.FindGameCts?.Token ?? CancellationToken.None);

                if (!findGameResult.Success)
                {
                    Log(LogLevel.Error, "Gamesession.gamefinder", "An error occurred during the S2S FindGame request", new { findGameResult.ErrorMsg });
                    BroadcastFFNotification(GameFinderFailedRoute, new GameFinderFailureDto { Reason = findGameResult.ErrorMsg });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException ex) when (ex.Message.Contains("disconnected")) //Player disconnected during matchmaking.
            {
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Gamesession.gamefinder", "An error occurred during the S2S FindGame request", ex);
                BroadcastFFNotification(GameFinderFailedRoute, new GameFinderFailureDto { Reason = ex.Message });
            }
            finally
            {
                await _partyState.TaskQueue.PushWork(async () =>
                {
                    await ResetMembersReadiness();

                    _partyState.FindGameRequest = null;
                    _partyState.FindGameCts?.Dispose();
                    _partyState.FindGameCts = null;
                });
            }
        }

        private async Task TryCancelPendingGameFinder()
        {
            if (_partyState.FindGameCts != null)
            {
                // In this case, the party members' status will be reset after the request is canceled.
                _partyState.FindGameCts.Cancel();
                return;
            }
            else
            {
                await ResetMembersReadiness();
            }
        }

        private async Task ResetMembersReadiness()
        {
            var update = new BatchStatusUpdate();
            foreach (var partyUser in _partyState.PartyMembers.Values)
            {
                if (partyUser.StatusInParty != PartyMemberStatus.NotReady)
                {
                    partyUser.StatusInParty = PartyMemberStatus.NotReady;
                    update.UserStatus.Add(new PartyMemberStatusUpdate { UserId = partyUser.UserId, Status = partyUser.StatusInParty });
                }
            }

            if (update.UserStatus.Count > 0)
            {
                await BroadcastStateUpdateRpc(BatchStatusUpdate.Route, update);
            }
        }

        /// <summary>
        /// This method should be called to notify party members when the party's state is updated.
        /// </summary>
        /// <remarks>This overload allows sending specific data per member.</remarks>
        /// <typeparam name="T">Type of the data to send to the party members.</typeparam>
        /// <param name="route">RPC route on which the data will be sent.</param>
        /// <param name="dataPerMember">The data to send to each member. In this overload, the data is member-specific.</param>
        /// <returns>Task that completes when every member has received the data, or when <c>_clientRpcTimeout</c> has been reached.</returns>
        private async Task BroadcastStateUpdateRpc<T>(string route, Dictionary<PartyMember, T> dataPerMember)
        {
            _partyState.VersionNumber++;

            if (_partyState.PartyMembers.IsEmpty)
            {
                return;
            }

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(_clientRpcTimeout);

                await Task.WhenAll(
                        dataPerMember.Where(kvp => kvp.Key.Peer != null).Select(kvp =>
                        {
                            try
                            {
                                if (kvp.Key.Peer != null)
                                {
                                    return _rpcService.Rpc(
                                        route,
                                        kvp.Key.Peer,
                                        s =>
                                        {
                                            kvp.Key.Peer.Serializer().Serialize(_partyState.VersionNumber, s);
                                            kvp.Key.Peer.Serializer().Serialize(kvp.Value, s);
                                        },
                                        PacketPriority.MEDIUM_PRIORITY,
                                        cts.Token
                                    ).LastOrDefaultAsync().ToTask()
                                    .ContinueWith(task =>
                                    {
                                        if (task.IsFaulted && !(task.Exception?.InnerException is OperationCanceledException))
                                        {
                                            Log(
                                                LogLevel.Trace,
                                                "BroadcastStateUpdateRpc",
                                                $"An error occurred during a client RPC (route: '{route}')",
                                                new { kvp.Key.UserId, kvp.Key.Peer.SessionId, task.Exception, Route = route },
                                                kvp.Key.UserId, kvp.Key.Peer.SessionId.ToString()
                                            );
                                        }
                                    });
                                }
                                else
                                {
                                    return Task.CompletedTask;
                                }
                            }
                            catch (Exception)
                            {
                                return Task.CompletedTask;
                            }

                        }) // dataPerMember.Select()
                    ); // Task.WhenAll()

            } // using cts
        }

        /// <summary>
        /// This method should be called to notify party members when the party's state is updated.
        /// </summary>
        /// <remarks>This overload sends the same data to all members.</remarks>
        /// <typeparam name="T">Type of the data to send to the party members.</typeparam>
        /// <param name="route">RPC route on which the data will be sent.</param>
        /// <param name="data">Data to send to the party members.</param>
        /// <returns></returns>
        private async Task BroadcastStateUpdateRpc<T>(string route, T data)
        {
            await BroadcastStateUpdateRpc(route, _partyState.PartyMembers.Values.ToDictionary(member => member, _ => data));
        }

        // This method should be used to broadcast a notification to party members that is not part of the party state.
        private void BroadcastFFNotification<T>(string route, T data)
        {
            var filter = new MatchArrayFilter(_partyState.PartyMembers.Select(p => p.Key));
            _scene.Send(filter, route, static (s, t) =>
            {
                var (serializer, data) = t;
                serializer.Serialize(data, s);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (_serializer, data));
        }

        public async Task SendPartyState(string recipientUserId, CancellationToken ct)
        {
            var handlers = _handlers();
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                PartyMember member;
                if (!TryGetMemberByUserId(recipientUserId, out member))
                {
                    return;
                }

                if (member.Peer != null)
                {
                    var state = await MakePartyStateDto(member, handlers);

                    using (var cts = new CancellationTokenSource())
                    {
                        cts.CancelAfter(_clientRpcTimeout);

                        await _rpcService.Rpc(
                            SendPartyStateRoute,
                            member.Peer,
                            s => member.Peer.Serializer().Serialize(state, s),
                            PacketPriority.HIGH_PRIORITY,
                            cts.Token
                            ).LastOrDefaultAsync().ToTask();
                    }
                }
            });
        }

        public async Task SendPartyStateAsRequestAnswer(RequestContext<IScenePeerClient> ctx)
        {
            var handlers = _handlers();
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (ctx.CancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }

                if (_partyState.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
                {
                    var dto = await MakePartyStateDto(member, handlers);

                    await ctx.SendValue(dto);
                    return;
                }
                else
                {
                    ThrowNoSuchMemberError(ctx.RemotePeer.SessionId);
                }
            });
        }

        private async Task<PartyStateDto> MakePartyStateDto(PartyMember recipient, IEnumerable<IPartyEventHandler> handlers)
        {
            var dto = new PartyStateDto
            {
                LeaderId = _partyState.Settings.PartyLeaderId,
                Settings = new PartySettingsUpdateDto(_partyState),
                PartyMembers = _partyState.PartyMembers.Values.Select(member =>
                    new PartyMemberDto { PartyUserStatus = member.StatusInParty, UserData = member.UserData, UserId = member.UserId, SessionId = member.SessionId, LocalPlayers = member.LocalPlayers, ConnectionStatus = member.ConnectionStatus }).ToList(),
                Version = _partyState.VersionNumber
            };

            await handlers.RunEventHandler(
                h => h.OnSendingSettingsUpdateToMembers(
                    new PartySettingsMemberUpdateCtx(
                        this,
                        new Dictionary<PartyMember,
                        PartySettingsUpdateDto> { { recipient, dto.Settings } }
                    )
                ),
                ex => Log(LogLevel.Error, "MakePartyStateDto", "An exception was thrown by a OnSendingSettingsUpdateToMembers handler")
            );

            return dto;
        }

        public async Task<bool> SendInvitation(SessionId senderSessionId, PlatformId recipientUserId, bool preferPlatformInvite, CancellationToken cancellationToken)
        {
            //Reimplement internal invitation.
            //throw new NotImplementedException();

            PartyMember? senderMember;
            if (!TryGetMemberBySessionId(senderSessionId, out senderMember))
            {
                ThrowNoSuchMemberError(senderSessionId);
            }

            
            var senderSession = await _userSessions.GetSessionById(senderSessionId, cancellationToken);

            if(senderSession == null)
            {
                throw new ClientException("disconnected");
            }
            var recipients = await _userSessions.GetDetailedUserInformationsByIdentityAsync(recipientUserId.Platform, new[] { recipientUserId.PlatformUserId },cancellationToken);

            InvitationContext ctx;
            if(recipients.TryGetValue(recipientUserId.PlatformUserId,out var sessionInfos))
            {
                ctx = new InvitationContext(this, senderSession, recipientUserId, sessionInfos.User, sessionInfos.Sessions, cancellationToken);
            }
            else
            {
                ctx = new InvitationContext(this, senderSession, recipientUserId, null, Enumerable.Empty<Session>(), cancellationToken);
            }

            if (preferPlatformInvite)
            {
                foreach (var handler in _platformSupports)
                {
                    if(handler.CanHandle(ctx))
                    {
                        return await handler.SendInvitation(ctx);
                    }
                }
            }
            var guid = new Guid();
            guid.ToString();
            if (ctx.RecipientUser == null)
            {
                ThrowNoSuchUserError(recipientUserId.PlatformUserId);
            }
            var result = await _userSessions.SendRequest<bool, string>("party.invite", senderMember.UserId, recipientUserId.PlatformUserId, PartyId, cancellationToken);

            if (!result.Success)
            {
                throw new ClientException(result.Error);
            }

            return result.Value;

            //var senderSession = await _userSessions.GetSession(senderMember.Peer, cancellationToken);

            //IPartyPlatformSupport? ChooseInvitationPlatform()
            //{
            //    if (forceStormancerInvite)
            //    {
            //        return _stormancerPartyPlatformSupport;
            //    }

            //    IPartyPlatformSupport? platform = null;
            //    // If the recipient is connected
            //    if (recipientSessions.Any())
            //    {
            //        // If they are on the same platform, choose this platform's handler in priority
            //        if (recipientSession.platformId.Platform == senderSession.platformId.Platform)
            //        {
            //            platform = _platformSupports.FirstOrDefault(platformSupport => platformSupport.PlatformName == senderSession.platformId.Platform);
            //        }
            //        // If they aren't, or if there is no handler for their platform, try a generic one (stormancer)
            //        if (platform == null)
            //        {
            //            platform = _platformSupports.FirstOrDefault(platformSupport =>
            //            platformSupport.IsInvitationCompatibleWith(recipientSession.platformId.Platform) && platformSupport.IsInvitationCompatibleWith(senderSession.platformId.Platform));
            //        }
            //    }
            //    else if (recipientUser != null)
            //    {
            //        if (recipientUser.Auth.ContainsKey(senderSession.platformId.Platform))
            //        {
            //            platform = _platformSupports.FirstOrDefault(platformSupport =>
            //            platformSupport.PlatformName == senderSession.platformId.Platform && platformSupport.CanSendInviteToDisconnectedPlayer);
            //        }
            //        if (platform == null)
            //        {
            //            platform = _platformSupports.FirstOrDefault(platformSupport =>
            //            platformSupport.CanSendInviteToDisconnectedPlayer &&
            //            recipientUser.Auth.Properties().Any(prop => platformSupport.IsInvitationCompatibleWith(prop.Name)) &&
            //            platformSupport.IsInvitationCompatibleWith(senderSession.platformId.Platform));
            //        }
            //    }
            //    return platform;
            //}

            //var platform = ChooseInvitationPlatform();
            //if (platform == null)
            //{
            //    Log(LogLevel.Error, "SendInvitation", "No suitable invitation platform found", new
            //    {
            //        senderUserId,
            //        recipientUserId,
            //        senderPlatformId = senderSession.platformId,
            //        recipientPlatformId = recipientSession?.platformId.Platform ?? "<N.A.: recipient is not online>",
            //        recipientAuth = recipientUser?.Auth.Properties().Select(prop => prop.Name),
            //        recipientIsOnline = recipientSession != null
            //    }, senderUserId, senderSession.SessionId.ToString(), recipientUserId);
            //    throw new Exception("No suitable invitation platform found");
            //}

            //// Do not block the party's TaskQueue.
            //var invitation = new Invitation(platform.PlatformName, cancellationToken);
            //await _partyState.TaskQueue.PushWork(async () =>
            //{
            //    if (TryGetMemberByUserId(recipientUserId, out _))
            //    {
            //        invitation.TaskCompletionSource.TrySetResult(true);
            //        return;
            //    }

            //    if (!_partyState.PendingInvitations.TryGetValue(recipientUserId, out var recipientInvitations))
            //    {
            //        recipientInvitations = new ConcurrentDictionary<string, Invitation>();
            //        _partyState.PendingInvitations.Add(recipientUserId, recipientInvitations);
            //    }

            //    if (recipientInvitations.TryGetValue(senderUserId, out var existingInvitation))
            //    {
            //        if (existingInvitation.PlatformName == platform.PlatformName)
            //        {
            //            invitation.TaskCompletionSource = existingInvitation.TaskCompletionSource;
            //            return;
            //        }
            //        else
            //        {
            //            existingInvitation.Cts.Cancel();
            //        }
            //    }
            //    recipientInvitations[senderUserId] = invitation;
            //    _ = platform.SendInvitation(new InvitationContext(this, senderSession, recipientUserId, invitation.Cts.Token))
            //    .ContinueWith(task =>
            //    {
            //        if (task.Exception != null)
            //        {
            //            invitation.TaskCompletionSource.TrySetException(task.Exception);
            //        }
            //        else if (task.IsCanceled)
            //        {
            //            invitation.TaskCompletionSource.TrySetCanceled();
            //        }
            //        else
            //        {
            //            invitation.TaskCompletionSource.TrySetResult(task.Result);
            //        }
            //        // This line here is the reason recipientInvitations is a concurrent dictionary. This may happen concurrently with the invitee's connection process that iterates over the dic.
            //        recipientInvitations.TryRemove(senderUserId, out _);
            //    });
            //    await Task.CompletedTask; // Silence warning
            //});

            //return await invitation.TaskCompletionSource.Task;
        }

        public bool CanSendInvitation(string senderUserId)
        {
            if (Settings.PartyLeaderId == senderUserId)
            {
                return true;
            }

            if (TryGetMemberByUserId(senderUserId, out _) && !Settings.OnlyLeaderCanInvite)
            {
                return true;
            }

            return false;
        }

        public Task<string> CreateInvitationCodeAsync(CancellationToken cancellationToken)
        {
            return invitationCodes.CreateCode(this._scene, cancellationToken);
        }

        public void CancelInvitationCode()
        {
            invitationCodes.CancelCode(this._scene);
        }

        public Task<Models.Party> GetModel()
        {
            var party = new Models.Party()
            {
                PartyId = this.PartyId,
                CreationTimeUtc = this.State.CreatedOnUtc,
                PartyLeaderId = this.Settings.PartyLeaderId,
                CustomData = this.Settings.CustomData,
                Players = new Dictionary<string, Models.Player>(),
                Platform = _partyState.Platform

            };

            foreach (var member in this.PartyMembers)
            {
                party.Players.Add(member.Value.UserId, new Models.Player(member.Value.SessionId, member.Value.UserId, member.Value.UserData) { LocalPlayers = member.Value.LocalPlayers });
            }

           
            return Task.FromResult(party);
        }

        public async Task<bool> CreateReservation(PartyReservation reservation)
        {
            return await _partyState.TaskQueue.PushWork(async () =>
            {
                if (_partyState.MemberCount + reservation.PartyMembers.Count() > _partyState.Settings.ServerSettings.MaxMembers())
                {
                    return false;
                }

                await using var scope = _scene.CreateRequestScope();
                var handlers = scope.Resolve<IEnumerable<IPartyEventHandler>>();

                var ctx = new CreateReservationContext(this, _scene, reservation);
                await handlers.RunEventHandler(h => h.OnCreatingReservation(ctx), ex => _logger.Log(LogLevel.Error, "party", $"An error occurred while running the event '{nameof(IPartyEventHandler.OnCreatingReservation)}'.", ex));

                if (!ctx.Accept)
                {
                    return false;
                }

                foreach (var r in reservation.PartyMembers)
                {

                    var partyUser = new PartyMember { UserId = r.UserId, SessionId = r.SessionId, StatusInParty = PartyMemberStatus.NotReady, Peer = null, UserData = r.Data, LocalPlayers = r.LocalPlayers ?? new List<Models.LocalPlayerInfos>(), ConnectionStatus = PartyMemberConnectionStatus.Reservation };
                    _partyState.PartyMembers.TryAdd(r.SessionId, partyUser);

                    await BroadcastStateUpdateRpc(MemberConnectedRoute, new PartyMemberDto { PartyUserStatus = partyUser.StatusInParty, UserData = partyUser.UserData, UserId = partyUser.UserId, SessionId = r.SessionId, LocalPlayers = partyUser.LocalPlayers, ConnectionStatus = PartyMemberConnectionStatus.Reservation });
                    _ = this.RemoveExpiredReservations(TimeSpan.FromSeconds(10), r.SessionId);

                }


                return true;

            });
        }
    }
}
