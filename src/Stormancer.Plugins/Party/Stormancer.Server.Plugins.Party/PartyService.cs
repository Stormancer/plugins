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

using Autofac.Core;
using Docker.DotNet.Models;
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
        private readonly IUserService _users;
        private readonly IEnumerable<IPartyPlatformSupport> _platformSupports;
        private readonly StormancerPartyPlatformSupport _stormancerPartyPlatformSupport;
        private readonly InvitationCodeService invitationCodes;
        private readonly PartyLuceneDocumentStore partyDocumentsStore;
        private readonly PartyConfigurationService partyConfigurationService;
        private readonly IProfileService _profiles;
        private readonly PartyAnalyticsWorker _analyticsWorker;

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
            StormancerPartyPlatformSupport stormancerPartyPlatformSupport,
            InvitationCodeService invitationCodes,
            PartyLuceneDocumentStore partyDocumentsStore,
            PartyConfigurationService partyConfigurationService,
            IProfileService profiles,
            PartyAnalyticsWorker analyticsWorker
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
            _users = users;
            _platformSupports = platformSupports;
            _stormancerPartyPlatformSupport = stormancerPartyPlatformSupport;
            this.invitationCodes = invitationCodes;
            this.partyDocumentsStore = partyDocumentsStore;
            this.partyConfigurationService = partyConfigurationService;
            _profiles = profiles;
            _analyticsWorker = analyticsWorker;
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

        private bool TryGetMemberByUserId(string userId, out PartyMember member)
        {
            member = _partyState.PartyMembers.FirstOrDefault(kvp => kvp.Value.UserId == userId).Value;
            return member != null;
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

                var session = await _userSessions.GetSession(peer, CancellationToken.None);
                if (session == null)
                {
                    throw new ClientException(JoinDeniedError + "?reason=notAuthenticated");
                }
                var userData = peer.ContentType == "party/userdata" ? peer.UserData : new byte[0];
                var ctx = new JoiningPartyContext(this, session, peer, _partyState.PendingAcceptedPeers.Count + _partyState.PartyMembers.Count, userData);
                await handlers.RunEventHandler(h => h.OnJoining(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running OnJoining", ex));
                if (!ctx.Accept)
                {
                    Log(LogLevel.Trace, "OnConnecting", "Join denied by event handler", peer.SessionId, session.User?.Id);
                    var message = JoinDeniedError;
                    if (!string.IsNullOrWhiteSpace(ctx.Reason))
                    {
                        message += $"?reason={ctx.Reason}";
                    }
                    throw new ClientException(message);
                }

                Log(LogLevel.Trace, "OnConnecting", "Join accepted", peer.SessionId, session.User?.Id);
                _partyState.PendingAcceptedPeers.Add(peer);
            });
        }

        internal async Task OnConnectionRejected(IScenePeerClient peer)
        {
            await _partyState.TaskQueue.PushWork(() =>
            {
                Log(LogLevel.Trace, "OnConnectionRejected", "Connection to party was rejected", peer.SessionId);
                _partyState.PendingAcceptedPeers.Remove(peer);
                _ = RunOperationCompletedEventHandler(async (service, handlers, scope) =>
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
                await RunOperationCompletedEventHandler(async (service, handlers, scope) =>
                {
                    var joinedCtx = new PreJoinedPartyContext(service, peer, session, userData);
                    await handlers.RunEventHandler(handler => handler.OnPreJoined(joinedCtx), exception =>
                    {
                        service.Log(LogLevel.Error, "OnConnected", "An exception was thrown by an OnJoined event handler", new { exception }, peer.SessionId.ToString(), user.Id);
                    });
                    errorId = joinedCtx.ErrorId;
                });


                if(errorId !=null)
                {
                    await Task.Delay(1000);
                    await peer.Disconnect(GenericJoinError+"?reason="+errorId);
                    return;
                }
              
               


                await _userSessions.UpdateSessionData(peer.SessionId, "party", _partyState.Settings.PartyId, CancellationToken.None);
              

                var profile = await _profiles.GetProfile(user.Id, new Dictionary<string, string> { ["user"] = "summary" }, session, CancellationToken.None);

                var mainLocalUser = new LocalPlayerInfos { StormancerUserId = user.Id, Platform = session.platformId.Platform, PlatformId = session.platformId.OnlineId, Pseudo = profile?["user"]?["pseudo"]?.ToObject<string>() ?? "anonymous" };

                var partyUser = new PartyMember { UserId = user.Id, StatusInParty = PartyMemberStatus.NotReady, Peer = peer, UserData = userData, LocalPlayers = new List<LocalPlayerInfos> { mainLocalUser } };
                _partyState.PartyMembers.TryAdd(peer.SessionId, partyUser);
                // Complete existing invitations for the new user. These invitations should all have been completed by now, but this is hard to guarantee.
                if (_partyState.PendingInvitations.TryGetValue(user.Id, out var invitations))
                {
                    foreach (var invitation in invitations)
                    {
                        invitation.Value.TaskCompletionSource.TrySetResult(true);
                    }
                    _partyState.PendingInvitations.Remove(user.Id);
                }





                var ClientPluginVersion = peer.Metadata[PartyPlugin.CLIENT_METADATA_KEY];
                Log(LogLevel.Trace, "OnConnected", "Connection complete", new { peer.SessionId, user.Id, ClientPluginVersion }, peer.SessionId.ToString(), user.Id);

                await BroadcastStateUpdateRpc(MemberConnectedRoute, new PartyMemberDto { PartyUserStatus = partyUser.StatusInParty, UserData = partyUser.UserData, UserId = partyUser.UserId, SessionId = partyUser.Peer.SessionId, LocalPlayers = partyUser.LocalPlayers });

                await RunOperationCompletedEventHandler((service, handlers, scope) =>
                {
                    var joinedCtx = new JoinedPartyContext(service,peer, session, partyUser.UserData);
                    return handlers.RunEventHandler(handler => handler.OnJoined(joinedCtx), exception =>
                    {
                        service.Log(LogLevel.Error, "OnConnected", "An exception was thrown by an OnJoined event handler", new { exception }, peer.SessionId.ToString(), user.Id);
                    });
                });
            });
        }

        private async Task RunOperationCompletedEventHandler(Func<PartyService, IEnumerable<IPartyEventHandler>, IDependencyResolver, Task> runner)
        {
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
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
                        await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(partyResetCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while processing an 'OnPlayerReadyStateRest' event.", ex));

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


                        await _userSessions.UpdateSessionData(args.Peer.SessionId, "party", null, CancellationToken.None);
                        await BroadcastStateUpdateRpc(PartyMemberDisconnection.Route, new PartyMemberDisconnection { UserId = partyUser.UserId, Reason = ParseDisconnectionReason(args.Reason) });


                    }
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

                _ = RunOperationCompletedEventHandler((service, handlers, scope) =>
                {
                    var ctx = new QuitPartyContext(service, args);
                    return handlers.RunEventHandler(handler => handler.OnQuit(ctx), exception => service.Log(LogLevel.Error, "OnDisconnected", "An exception was thrown by an OnQuit event handler", new { exception }, args.Peer.SessionId.ToString()));
                });
            });
        }

        public void SetConfiguration(dynamic metadata)
        {
            if (metadata.party != null)
            {

                _partyState.Settings = metadata.party.ToObject<PartyConfiguration>()!;
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

                    await handlers.RunEventHandler(h => h.OnUpdatingSettings(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while running OnUpdatingSettings", ex));

                    if (!ctx.ApplyChanges)
                    {
                        Log(LogLevel.Trace, "UpdateSettings", "Settings update refused by event handler", partySettingsDto);
                        throw new ClientException(ctx.ErrorMsg);
                    }

                   
                    await handlers.RunEventHandler(h => h.OnUpdateSettings(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while running OnUpdateSettings", ex));

                    // If the event handlers have modified the settings, we need to notify the leader to invalidate their local copy.
                    // Make an additional bump to the version number to achieve this.
                    int newSettingsVersion = _partyState.SettingsVersionNumber + 1;
                    if (!partySettingsDto.Equals(originalDto))
                    {
                        newSettingsVersion = _partyState.SettingsVersionNumber + 2;
                    }

                   
                    _partyState.Settings.GameFinderName = partySettingsDto.GameFinderName;
                    _partyState.Settings.CustomData = partySettingsDto.CustomData;
                    _partyState.Settings.OnlyLeaderCanInvite = partySettingsDto.OnlyLeaderCanInvite;
                    _partyState.Settings.IsJoinable = partySettingsDto.IsJoinable;


                    if (!string.IsNullOrEmpty(partySettingsDto.IndexedDocument))
                    {
                        try
                        {
                            _partyState.SearchDocument = JObject.Parse(partySettingsDto.IndexedDocument);
                        }
                        catch (Exception)
                        {
                            _partyState.SearchDocument = null;
                            //Ignore parse errors.
                        }
                    }
                    else
                    {
                        _partyState.SearchDocument = null;
                    }

                    if (partySettingsDto.PublicServerData != null)
                    {
                        _partyState.Settings.PublicServerData = partySettingsDto.PublicServerData;
                    }
                    _partyState.SettingsVersionNumber = newSettingsVersion;
                    //Log(LogLevel.Info, "UpdateSettings", $"Updated public server data to party='{this.PartyId}' Version={newSettingsVersion}", partySettingsDto.PublicServerData);

                    var partyResetCtx = new PartyMemberReadyStateResetContext(PartyMemberReadyStateResetEventType.PartySettingsUpdated, _scene);
                    partyConfigurationService.ShouldResetPartyMembersReadyState(partyResetCtx);
                    await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(partyResetCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while processing an 'OnPlayerReadyStateRest' event.", ex));

                    if (partyResetCtx.ShouldReset)
                    {
                        await TryCancelPendingGameFinder();
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
            return UpdateSettings(_ => partySettingsDto, ct);

        }

        /// <summary>
        /// Player status 
        /// </summary>
        /// <param name="partyUserStatus"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task UpdateGameFinderPlayerStatus(string userId, PartyMemberStatusUpdateRequest partyUserStatus, CancellationToken ct)
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
                if (ct.IsCancellationRequested)
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

                var updatingCtx = new UpdatingPlayerReadyStateContext(this, user, _scene);

                await handlers.RunEventHandler(h => h.OnUpdatingPlayerReadyState(updatingCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while running 'OnUpdatingPlayerReadyState'", ex));

                if (!updatingCtx.Accept)
                {
                    throw new ClientException(string.IsNullOrEmpty(updatingCtx.ErrorId) ? "party.ready.updateDenied" : updatingCtx.ErrorId);
                }

                user.StatusInParty = partyUserStatus.DesiredStatus;
                Log(LogLevel.Trace, "UpdateGameFinderPlayerStatus", $"Updated user status, new value: {partyUserStatus}", user.Peer.SessionId, user.UserId);

                var update = new BatchStatusUpdate();
                update.UserStatus.Add(new PartyMemberStatusUpdate { UserId = userId, Status = user.StatusInParty });
                await BroadcastStateUpdateRpc(BatchStatusUpdate.Route, update);

                var eventHandlerCtx = new PlayerReadyStateContext(this, user, _scene);
                await handlers.RunEventHandler(h => h.OnPlayerReadyStateChanged(eventHandlerCtx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while running OnPlayerReadyStateChanged", ex));

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

                if (shouldLaunchGameFinderRequest)
                {
                    Log(LogLevel.Trace, "UpdateGameFinderPlayerStatus", "Launching a FindGame request");
                    LaunchGameFinderRequest();
                }
                else if (IsGameFinderRunning)
                {
                    await TryCancelPendingGameFinder();
                }
            });
        }

        private bool IsGameFinderRunning
        {
            get
            {
                return _partyState.FindGameRequest != null;
            }
        }

        public ConcurrentDictionary<string, object> ServerData => _partyState.ServerData;

        public string PartyId => Settings.PartyId;

        public async Task UpdatePartyUserData(string userId, byte[] data, List<LocalPlayerInfos> localPlayers, CancellationToken ct)
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

                var ctx = new UpdatingPartyMemberDataContext(partyUser, localPlayers, data, _scene,this);
                await handlers.RunEventHandler(h => h.OnUpdatingPartyMemberData(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occurred while running the event 'OnUpdatingPartyMemberData'.", ex));

                if (!ctx.IsUpdateValid)
                {
                    throw new ClientException(ctx.Error ?? "party.invalidMemberData");
                }



                partyUser.LocalPlayers = localPlayers;
                partyUser.UserData = data;
                Log(LogLevel.Trace, "UpdatePartyUserData", "Updated user data", new { partyUser.Peer.SessionId, partyUser.UserId, UserData = data });

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

                    await BroadcastStateUpdateRpc(PartyMemberDataUpdate.Route, new PartyMemberDataUpdate { UserId = userId, UserData = partyUser.UserData, LocalPlayers = partyUser.LocalPlayers });
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
                Log(LogLevel.Trace, "PromoteLeader", $"Promoted new leader, userId: {user.UserId}", user.Peer.SessionId, user.UserId);

                await BroadcastStateUpdateRpc(LeaderChangedRoute, _partyState.Settings.PartyLeaderId);
            });
        }

        public async Task KickPlayerByLeader(string playerToKick, CancellationToken ct)
        {
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (TryGetMemberByUserId(playerToKick, out var partyUser))
                {
                    if (playerToKick == _partyState.Settings.PartyLeaderId)
                    {
                        throw new ClientException(CannotKickLeaderError);
                    }
                    var handlers = _handlers();
                    var ctx = new PartyMemberReadyStateResetContext(PartyMemberReadyStateResetEventType.PartyMembersListUpdated, _scene);
                    partyConfigurationService.ShouldResetPartyMembersReadyState(ctx);
                    await handlers.RunEventHandler(h => h.OnPlayerReadyStateReset(ctx), ex => _logger.Log(LogLevel.Error, "party", "An error occured while processing an 'OnPlayerReadyStateRest' event.", ex));

                    if (ctx.ShouldReset)
                    {
                        await TryCancelPendingGameFinder();
                    }


                    _partyState.PartyMembers.TryRemove(partyUser.Peer.SessionId, out _);


                    Log(LogLevel.Trace, "KickPlayerByLeader", $"Kicked a player, userId: {partyUser.UserId}", partyUser.Peer.SessionId, partyUser.UserId);

                    await partyUser.Peer.Disconnect("party.kicked");
                    await BroadcastStateUpdateRpc(PartyMemberDisconnection.Route, new PartyMemberDisconnection { UserId = partyUser.UserId, Reason = PartyDisconnectionReason.Kicked });
                }
                // Do not return an error if the player is already gone
            });
        }

        private void LaunchGameFinderRequest()
        {
            if (!IsGameFinderRunning)
            {
                _partyState.FindGameCts = new CancellationTokenSource();
                _partyState.FindGameRequest = FindGame_Impl();
            }
        }

        private async Task FindGame_Impl()
        {

            //Construct gameFinder request
            var gameFinderRequest = new Models.Party();
            gameFinderRequest.CustomData = _partyState.Settings.CustomData;
            gameFinderRequest.PartyId = _partyState.Settings.PartyId;
            gameFinderRequest.PartyLeaderId = _partyState.Settings.PartyLeaderId;
            foreach (var partyUser in _partyState.PartyMembers.Values)
            {
                gameFinderRequest.Players.Add(partyUser.UserId, new Models.Player(partyUser.Peer.SessionId.ToString(), partyUser.UserId, partyUser.UserData));

            }

            gameFinderRequest.CustomData = _partyState.Settings.CustomData;

            //Send S2S find match request
            try
            {
                //var sceneUri = await _locator.GetSceneId("stormancer.plugins.gamefinder", );
                var findGameResult = await _gameFinderClient.FindGame(_partyState.Settings.GameFinderName, gameFinderRequest, _partyState.FindGameCts?.Token ?? CancellationToken.None);

                if (!findGameResult.Success)
                {
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
                Log(LogLevel.Error, "FindGame_Impl", "An error occurred during the S2S FindGame request", ex);
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

                try
                {
                    await Task.WhenAll(
                        dataPerMember.Select(kvp =>
                            _rpcService.Rpc(
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
                            })
                        ) // dataPerMember.Select()
                    ); // Task.WhenAll()
                }
                catch(Exception)
                {
                    //Ignore if a peer isn't connected anymore to the scene.
                }
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
            _scene.Broadcast(route, data);
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
                    ThrowNoSuchMemberError(recipientUserId);
                }

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
                    new PartyMemberDto { PartyUserStatus = member.StatusInParty, UserData = member.UserData, UserId = member.UserId, SessionId = member.Peer.SessionId, LocalPlayers = member.LocalPlayers }).ToList(),
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

        public async Task<bool> SendInvitation(string senderUserId, string recipientUserId, bool forceStormancerInvite, CancellationToken cancellationToken)
        {
            PartyMember senderMember;
            if (!TryGetMemberByUserId(senderUserId, out senderMember))
            {
                ThrowNoSuchMemberError(senderUserId);
            }

            User? recipientUser = null;
            var recipientSession = await _userSessions.GetSessionByUserId(recipientUserId, cancellationToken);
            if (recipientSession == null)
            {
                recipientUser = await _users.GetUser(recipientUserId);
                if (recipientUser == null)
                {
                    ThrowNoSuchUserError(recipientUserId);
                }
            }

            var senderSession = await _userSessions.GetSession(senderMember.Peer, cancellationToken);

            IPartyPlatformSupport? ChooseInvitationPlatform()
            {
                if (forceStormancerInvite)
                {
                    return _stormancerPartyPlatformSupport;
                }

                IPartyPlatformSupport? platform = null;
                // If the recipient is connected
                if (recipientSession != null)
                {
                    // If they are on the same platform, choose this platform's handler in priority
                    if (recipientSession.platformId.Platform == senderSession.platformId.Platform)
                    {
                        platform = _platformSupports.FirstOrDefault(platformSupport => platformSupport.PlatformName == senderSession.platformId.Platform);
                    }
                    // If they aren't, or if there is no handler for their platform, try a generic one (stormancer)
                    if (platform == null)
                    {
                        platform = _platformSupports.FirstOrDefault(platformSupport =>
                        platformSupport.IsInvitationCompatibleWith(recipientSession.platformId.Platform) && platformSupport.IsInvitationCompatibleWith(senderSession.platformId.Platform));
                    }
                }
                else if (recipientUser != null)
                {
                    if (recipientUser.Auth.ContainsKey(senderSession.platformId.Platform))
                    {
                        platform = _platformSupports.FirstOrDefault(platformSupport =>
                        platformSupport.PlatformName == senderSession.platformId.Platform && platformSupport.CanSendInviteToDisconnectedPlayer);
                    }
                    if (platform == null)
                    {
                        platform = _platformSupports.FirstOrDefault(platformSupport =>
                        platformSupport.CanSendInviteToDisconnectedPlayer &&
                        recipientUser.Auth.Properties().Any(prop => platformSupport.IsInvitationCompatibleWith(prop.Name)) &&
                        platformSupport.IsInvitationCompatibleWith(senderSession.platformId.Platform));
                    }
                }
                return platform;
            }

            var platform = ChooseInvitationPlatform();
            if (platform == null)
            {
                Log(LogLevel.Error, "SendInvitation", "No suitable invitation platform found", new
                {
                    senderUserId,
                    recipientUserId,
                    senderPlatformId = senderSession.platformId,
                    recipientPlatformId = recipientSession?.platformId.Platform ?? "<N.A.: recipient is not online>",
                    recipientAuth = recipientUser?.Auth.Properties().Select(prop => prop.Name),
                    recipientIsOnline = recipientSession != null
                }, senderUserId, senderSession.SessionId.ToString(), recipientUserId);
                throw new Exception("No suitable invitation platform found");
            }

            // Do not block the party's TaskQueue.
            var invitation = new Invitation(platform.PlatformName, cancellationToken);
            await _partyState.TaskQueue.PushWork(async () =>
            {
                if (TryGetMemberByUserId(recipientUserId, out _))
                {
                    invitation.TaskCompletionSource.TrySetResult(true);
                    return;
                }

                if (!_partyState.PendingInvitations.TryGetValue(recipientUserId, out var recipientInvitations))
                {
                    recipientInvitations = new ConcurrentDictionary<string, Invitation>();
                    _partyState.PendingInvitations.Add(recipientUserId, recipientInvitations);
                }

                if (recipientInvitations.TryGetValue(senderUserId, out var existingInvitation))
                {
                    if (existingInvitation.PlatformName == platform.PlatformName)
                    {
                        invitation.TaskCompletionSource = existingInvitation.TaskCompletionSource;
                        return;
                    }
                    else
                    {
                        existingInvitation.Cts.Cancel();
                    }
                }
                recipientInvitations[senderUserId] = invitation;
                _ = platform.SendInvitation(new InvitationContext(this, senderSession, recipientUserId, invitation.Cts.Token))
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        invitation.TaskCompletionSource.TrySetException(task.Exception);
                    }
                    else if (task.IsCanceled)
                    {
                        invitation.TaskCompletionSource.TrySetCanceled();
                    }
                    else
                    {
                        invitation.TaskCompletionSource.TrySetResult(task.Result);
                    }
                    // This line here is the reason recipientInvitations is a concurrent dictionary. This may happen concurrently with the invitee's connection process that iterates over the dic.
                    recipientInvitations.TryRemove(senderUserId, out _);
                });
                await Task.CompletedTask; // Silence warning
            });

            return await invitation.TaskCompletionSource.Task;
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


    }
}
