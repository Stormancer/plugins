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
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Context of a Gamefinding pass.
    /// </summary>
    public class GameFinderContext
    {
        /// <summary>
        /// Parties in the queue.
        /// </summary>
        public List<Party> WaitingClient { get; set; } = new List<Party>();

        /// <summary>
        /// List of parties which failed matchmaking during this pass.
        /// </summary>
        public List<(Party client, string reason)> FailedClients { get; set; } = new List<(Party client, string reason)>();

        /// <summary>
        /// Sets a party as having failed matchmaking.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="reason"></param>
        public void SetFailed(Party party, string reason)
        {
            FailedClients.Add((party, reason));
        }
        
        /// <summary>
        /// Game Sessions that are open to new players.
        /// </summary>
        /// <seealso cref="GameFinderController.OpenGameSession(JObject, RequestContext{IScenePeer})"/>
        public List<OpenGameSession> OpenGameSessions { get; } = new List<OpenGameSession>();
    }

    internal class GameFinderService : IGameFinderService
    {
        private const string UPDATE_NOTIFICATION_ROUTE = "gamefinder.update";
        private const string UPDATE_READYCHECK_ROUTE = "gamefinder.ready.update";
        private const string UPDATE_FINDGAME_REQUEST_PARAMS_ROUTE = "gamefinder.parameters.update";
        private const string LOG_CATEGORY = "GameFinderService";

        public const long ProtocolVersion = 2020_01_10_1;

        private ISceneHost _scene;

        private readonly IEnumerable<IGameFinderDataExtractor> _extractors;
        private readonly Func<IEnumerable<IGameFinderEventHandler>> handlers;
        private readonly IAnalyticsService analytics;
        private readonly IGameFinder _gameFinder;
        private readonly IGameFinderResolver _resolver;
        private readonly ILogger _logger;
        private readonly ISerializer _serializer;
        private readonly GameFinderData _data;

        // GameFinder Configuration
        public bool IsRunning { get => _data.IsRunning; private set => _data.IsRunning = value; }

        public GameFinderService(ISceneHost scene,
            IEnumerable<IGameFinderDataExtractor> extractors,
            Func<IEnumerable<IGameFinderEventHandler>> handlers,
            IAnalyticsService analytics,
            IEnvironment env,
            IGameFinder gameFinder,
            IGameFinderResolver resolver,
            ILogger logger,
            IConfiguration config,
            ISerializer serializer,
            GameFinderData data)
        {
            _extractors = extractors;
            this.handlers = handlers;
            this.analytics = analytics;
            _gameFinder = gameFinder;
            _resolver = resolver;
            _logger = logger;
            _serializer = serializer;
            _data = data;
            _scene = scene;
            Init(scene);
            env.ActiveDeploymentChanged += Env_ActiveDeploymentChanged;
            config.SettingsChanged += (s, c) => ApplyConfig(c);
            ApplyConfig(config.Settings);

            scene.Disconnected.Add(args => CancelGame(args.Peer, false));
            scene.AddProcedure("gamefinder.find", FindGame);
            scene.AddRoute("gamefinder.ready.resolve", ResolveReadyRequest, r => r);
            scene.AddRoute("gamefinder.cancel", CancelGame, r => r);
        }

        private void Env_ActiveDeploymentChanged(object? sender, ActiveDeploymentChangedEventArgs e)
        {
            if (!e.IsActive)
            {
                _data.acceptRequests = false;
                _ = CancelAll();
            }
        }

        private void ApplyConfig(dynamic config)
        {
            if (_data.kind == null || config == null)
            {
                _logger.Log(LogLevel.Error, LOG_CATEGORY, "GameFinder service can't find gameFinder kind or server application config", new { gameFinderKind = _data.kind });
                return;
            }

            var gameFinderConfigs = (JObject?)config?.gamefinder?.configs;
            dynamic? specificConfig = gameFinderConfigs?.GetValue(_data.kind);

            _data.interval = TimeSpan.FromSeconds((double)(specificConfig?.interval ?? 1));
            _data.isReadyCheckEnabled = (bool?)specificConfig?.readyCheck?.enabled ?? false;
            _data.readyCheckTimeout = (int)(specificConfig?.readyCheck?.timeout ?? 1000);

            foreach (var extractor in _extractors)
            {
                extractor.RefreshConfig(specificConfig);
            }

            _gameFinder.RefreshConfig(specificConfig, config);
            _resolver.RefreshConfig(specificConfig);
        }

        // This function called from GameFinder plugin
        public void Init(ISceneHost gameFinderScene)
        {
            _data.kind = gameFinderScene.Metadata[GameFinderPlugin.METADATA_KEY];

            _logger.Log(LogLevel.Trace, LOG_CATEGORY, "Initializing the GameFinderService.", new { extractors = _extractors.Select(e => e.GetType().ToString()) });

            if (this._scene != null)
            {
                throw new InvalidOperationException("The gameFinder service may only be initialized once.");
            }

            
        }

        public async Task FindGameS2S(RequestContext<IScenePeer> requestS2S)
        {
            if (!_data.acceptRequests)
            {
                throw new ClientException("gamefinder.disabled?reason=deploymentNotActive");
            }
            var party = new Party();
            var provider = _serializer.Deserialize<string>(requestS2S.InputStream);

            try
            {
                foreach (var extractor in _extractors)
                {
                    if (await extractor.ExtractDataS2S(provider, requestS2S.InputStream, party))
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Failed));
                throw;
            }

            PlayerPeer[]? peersInGroup = null;
            using (var scope = _scene.DependencyResolver.CreateChild(Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                peersInGroup = await Task.WhenAll(party.Players.Select(async p => new PlayerPeer { Peer = await sessions.GetPeer(p.Value.UserId), Player = p.Value }));
            }
            var state = new GameFinderRequestState(party);

            try
            {
                foreach (var p in peersInGroup)
                {
                    if (p.Peer == null)
                    {
                        throw new ClientException($"'{p.Player.UserId} has disconnected.");
                    }
                    //If player already waiting just replace infos instead of failing
                    //if (_data.peersToGroup.ContainsKey(p.Peer.Id))
                    //{
                    //    throw new ClientException($"'{p.Player.UserId} is already waiting for a game.");
                    //}
                }

                _data.waitingGroups[party] = state;
                foreach (var p in peersInGroup)
                {
                    _data.peersToGroup[p.Peer.SessionId] = party;
                }

                requestS2S.CancellationToken.Register(() =>
                {
                    state.Tcs.TrySetCanceled();
                });

                var memStream = new MemoryStream();
                requestS2S.InputStream.Seek(0, SeekOrigin.Begin);
                requestS2S.InputStream.CopyTo(memStream);
                await BroadcastToPlayers(party, UPDATE_FINDGAME_REQUEST_PARAMS_ROUTE, (s, sz) =>
                {
                    memStream.Seek(0, System.IO.SeekOrigin.Begin);
                    memStream.CopyTo(s);
                });
                await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                {
                    s.WriteByte((byte)GameFinderStatusUpdate.SearchStart);

                });
                state.State = RequestState.Ready;
            }
            catch (Exception ex)
            {
                state.Tcs.SetException(ex);
                await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Failed));

            }

            try
            {
                await state.Tcs.Task;
            }
            catch (TaskCanceledException)
            {
                await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Cancelled));
            }
            finally //Always remove party from list.
            {
              
                foreach (var p in peersInGroup)
                {
                  
                    if (p?.Peer?.SessionId != null)
                    {
                        _data.peersToGroup.TryRemove(p.Peer.SessionId, out _);
                    }
                }
               
                if (_data.waitingGroups.TryRemove(party, out var group) && group.Candidate != null)
                {
                    if (_pendingReadyChecks.TryGetValue(group.Candidate.Id, out var rc))
                    {
                        if (!rc.RanToCompletion)
                        {
                            // Todo jojo What can i do with this ?
                            //rc.Cancel(currentUser.Id);
                        }
                    }
                }
            }
        }

        public async Task FindGame(RequestContext<IScenePeerClient> request)
        {
            if (!_data.acceptRequests)
            {
                throw new ClientException("gamefinder.disabled?reason=deploymentNotActive");
            }

            var party = new Party();
            var provider = request.ReadObject<string>();

            User currentUser = null;
            using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                currentUser = await sessions.GetUser(request.RemotePeer);
            }
            foreach (var extractor in _extractors)
            {
                if (await extractor.ExtractData(provider, request, party))
                {
                    break;
                }
            }
            if (!party.Players.Any())
            {
                party.Players.Add(currentUser.Id, new Player(request.RemotePeer.SessionId, currentUser.Id) { });
            }

            PlayerPeer[] peersInGroup = null;
            using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                peersInGroup = await Task.WhenAll(party.Players.Select(async p => new PlayerPeer { Peer = await sessions.GetPeer(p.Value.UserId), Player = p.Value }));
            }

            foreach (var p in peersInGroup)
            {
                if (p.Peer == null)
                {
                    throw new ClientException($"'{p.Player.UserId} has disconnected.");
                }
                if (_data.peersToGroup.ContainsKey(p.Peer.SessionId))
                {
                    throw new ClientException($"'{p.Player.UserId} is already waiting for a game.");
                }
            }

            var state = new GameFinderRequestState(party);

            _data.waitingGroups[party] = state;
            foreach (var p in peersInGroup)
            {
                _data.peersToGroup[p.Peer.SessionId] = party;
            }

            request.CancellationToken.Register(() =>
            {
                state.Tcs.TrySetCanceled();
            });

            var memStream = new MemoryStream();
            request.InputStream.Seek(0, SeekOrigin.Begin);
            request.InputStream.CopyTo(memStream);
            await BroadcastToPlayers(party, UPDATE_FINDGAME_REQUEST_PARAMS_ROUTE, (s, sz) =>
            {
                memStream.Seek(0, System.IO.SeekOrigin.Begin);
                memStream.CopyTo(s);
            });
            await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
            {
                s.WriteByte((byte)GameFinderStatusUpdate.SearchStart);

            });
            state.State = RequestState.Ready;

            try
            {
                await state.Tcs.Task;
            }
            catch (TaskCanceledException)
            {
                await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Cancelled));

            }
            catch (ClientException ex)
            {
                await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                {
                    s.WriteByte((byte)GameFinderStatusUpdate.Failed);
                    sz.Serialize(ex.Message, s);
                });
            }
            finally //Always remove party from list.
            {
                GameFinderRequestState _;
                foreach (var p in peersInGroup)
                {
                    Party grp1;
                    _data.peersToGroup.TryRemove(p.Peer.SessionId, out grp1);
                }
                _data.waitingGroups.TryRemove(party, out _);
                if (_.Candidate != null)
                {
                    if (_pendingReadyChecks.TryGetValue(_.Candidate.Id, out var rc))
                    {
                        if (!rc.RanToCompletion)
                        {
                            rc.Cancel(currentUser.Id);
                        }
                    }
                }
            }
        }

        public async Task Run(CancellationToken ct)
        {
            IsRunning = true;

            var watch = new Stopwatch();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    watch.Restart();
                    await this.FindGamesOnce();
                    watch.Stop();

                    var playersNum = this._data.waitingGroups.Where(kvp => kvp.Value.State == RequestState.Ready).Sum(kvp => kvp.Key.Players.Count);
                    var groupsNum = this._data.waitingGroups.Where(kvp => kvp.Value.State == RequestState.Ready).Count();
                    //_logger.Log(LogLevel.Trace, $"{LOG_CATEGORY}.Run", $"A {_data.kind} pass was run for {playersNum} players and {groupsNum} parties", new
                    //{
                    //    Time = watch.Elapsed,
                    //    playersWaiting = playersNum,
                    //    groupsWaiting = groupsNum
                    //});
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Error, LOG_CATEGORY, "An error occurred while running a gameFinder.", e);
                }
                await Task.Delay(this._data.interval);
            }
            IsRunning = false;
        }

        private async Task FindGamesOnce()
        {
            var waitingClients = _data.waitingGroups.Where(kvp => kvp.Value.State == RequestState.Ready).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            try
            {

                foreach (var value in waitingClients.Values)
                {
                    value.State = RequestState.Searching;
                    value.Candidate = null;
                }

                GameFinderContext mmCtx = new GameFinderContext();
                mmCtx.WaitingClient.AddRange(waitingClients.Keys);
                mmCtx.OpenGameSessions.AddRange(_data.openGameSessions.Values.Where(ogs => ogs.IsOpen));

                var games = await this._gameFinder.FindGames(mmCtx);

                analytics.Push("gameFinder", "pass", JObject.FromObject(new
                {
                    type = _data.kind,
                    playersWaiting = _data.waitingGroups.SelectMany(kvp => kvp.Key.Players).Count(),
                    parties = _data.waitingGroups.Count(),
                    customData = _gameFinder.ComputeDataAnalytics(mmCtx),
                    openGameSessions = _data.openGameSessions.Count
                }));

                foreach ((Party party, string reason) in mmCtx.FailedClients)
                {
                    var client = waitingClients[party];
                    client.State = RequestState.Rejected;
                    // If this party is an S2S party, the exception will be forwarded to the S2S caller which is responsible for handling it
                    client.Tcs.TrySetException(new ClientException(reason));
                    // Remove games that contain a rejected party
                    games.Games.RemoveAll(m => m.AllParties.Contains(party));
                }

                if (games.Games.Any() || games.GameSessionTickets.Any())
                {
                    //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Prepare resolutions {waitingClients.Count} players for {matches.Matches.Count} matches.", new { waitingCount = waitingClients.Count });
                    await _resolver.PrepareGameResolution(games);
                }

                foreach (var game in games.Games)
                {
                    foreach (var party in game.Teams.SelectMany(t => t.Parties)) //Set game found to prevent players from being gameed again
                    {
                        var state = waitingClients[party];
                        state.State = RequestState.Found;
                        state.Candidate = game;
                    }

                    //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Resolve game for {waitingClients.Count} players", new { waitingCount = waitingClients.Count, currentGame = game });
                    _ = ResolveGameFound(game, waitingClients); // Resolve game, but don't wait for completion.
                    //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Resolve complete game for {waitingClients.Count} players", new { waitingCount = waitingClients.Count, currentGame = game });
                }
                foreach (var ticket in games.GameSessionTickets)
                {
                    foreach (var party in ticket.Teams.SelectMany(t => t.Parties))
                    {
                        var state = waitingClients[party];
                        state.State = RequestState.Found;
                        state.Candidate = ticket;
                    }

                    await ticket.GameSession.RegisterTeams(ticket.Teams);
                    _logger.Log(LogLevel.Trace, $"{LOG_CATEGORY}.FindGamesOnce", $"Registered {ticket.Teams.Count} teams for the open game session {ticket.Id}", new { GameSessionId = ticket.Id, ticket.Teams, ticket.GameSession.Data });

                    _ = ResolveGameFound(ticket, waitingClients);
                }

                foreach (var session in _data.openGameSessions.Values)
                {
                    session.NumGameFinderPasses++;
                    if (!session.IsOpen)
                    {
                        session.Complete();
                    }
                }
            }
            finally
            {
                foreach (var value in waitingClients.Values.Where(v => v.State == RequestState.Searching))
                {
                    value.State = RequestState.Ready;
                    value.Party.PastPasses++;
                }
            }
        }

        private async Task ResolveGameFound(IGameCandidate gameCandidate, Dictionary<Party, GameFinderRequestState> waitingClients)
        {
            try
            {
                Func<IGameFinderResolutionWriterContext, Task>? resolutionAction = null;
                string? gameSceneId = null;
                // I do not use 'if (gameCandidate is Game game)' so that 'game' does not leak in the outer scope
                if (gameCandidate is Game)
                {
                    var game = (Game)gameCandidate;
                    var resolverCtx = new GameResolverContext(game);
                    await _resolver.ResolveGame(resolverCtx);
                    resolutionAction = resolverCtx.ResolutionAction;
                    gameSceneId = resolverCtx.GameSceneId;

                    var ctx = new GameStartedContext();
                    ctx.GameFinderId = this._scene.Id;
                    ctx.Game = game;
                    await handlers().RunEventHandler(h => h.OnGameStarted(ctx), ex => { });
                }
                else if (gameCandidate is OpenGameSessionTicket)
                {
                    var ticket = (OpenGameSessionTicket)gameCandidate;
                    var ctx = new JoinOpenGameContext(ticket);
                    await _resolver.ResolveJoinOpenGame(ctx);
                    resolutionAction = ctx.ResolutionAction;
                    gameSceneId = ctx.GameSessionTicket.Id;
                }

                if (_data.isReadyCheckEnabled)
                {
                    await BroadcastToPlayers(gameCandidate, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                    {

                        s.WriteByte((byte)GameFinderStatusUpdate.WaitingPlayersReady);

                    });

                    using (var gameReadyCheckState = CreateReadyCheck(gameCandidate))
                    {
                        gameReadyCheckState.StateChanged += update =>
                        {
                            BroadcastToPlayers(gameCandidate, UPDATE_READYCHECK_ROUTE, (s, sz) =>
                            {
                                sz.Serialize(update, s);
                            });
                        };
                        var result = await gameReadyCheckState.WhenCompleteAsync();

                        if (!result.Success)
                        {
                            foreach (var party in result.UnreadyGroups)//Cancel gameFinder for timeouted parties
                            {
                                GameFinderRequestState mrs;
                                if (_data.waitingGroups.TryGetValue(party, out mrs))
                                {
                                    mrs.Tcs.TrySetCanceled();
                                }
                            }
                            foreach (var party in result.ReadyGroups)//Put ready parties back in queue.
                            {
                                GameFinderRequestState mrs;
                                if (_data.waitingGroups.TryGetValue(party, out mrs))
                                {
                                    mrs.State = RequestState.Ready;
                                    await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                                    {
                                        s.WriteByte((byte)GameFinderStatusUpdate.SearchStart);
                                    });

                                }
                            }
                            return; //stop here
                        }
                    }
                }

                foreach (var player in await GetPlayers(gameCandidate.AllParties().ToArray()))
                {
                    try
                    {
                        using (var stream = new MemoryStream())
                        {
                            var writerContext = new GameFinderResolutionWriterContext(player.Serializer(), stream, player);
                            // Write the connection token first, if a scene was created by the resolver, or if joining an existing session
                            if (!string.IsNullOrEmpty(gameSceneId))
                            {
                                using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                                {
                                    var gameSessions = scope.Resolve<IGameSessions>();
                                    var token = ClientSupportsV3Token(player) switch
                                    {
                                        true => await gameSessions.CreateConnectionToken(gameSceneId, player.SessionId, TokenVersion.V3),
                                        false => await gameSessions.CreateConnectionToken(gameSceneId, player.SessionId, TokenVersion.V1)
                                    };
                                    writerContext.WriteObjectToStream(token);
                                }
                            }
                            else
                            {
                                // Empty connection token, to avoid breaking deserialization client-side
                                writerContext.WriteObjectToStream("");
                            }
                            if (resolutionAction != null)
                            {
                                await resolutionAction(writerContext);
                            }
                            await _scene.Send(new MatchPeerFilter(player.SessionId), UPDATE_NOTIFICATION_ROUTE, s =>
                            {
                                s.WriteByte((byte)GameFinderStatusUpdate.Success);
                                stream.Seek(0, SeekOrigin.Begin);
                                stream.CopyTo(s);
                            }
                            , PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "gamefinder", "An error occured while trying to resolve a game for a player", ex);
                        await _scene.Send(new MatchPeerFilter(player.SessionId), UPDATE_NOTIFICATION_ROUTE, s =>
                        {
                            s.WriteByte((byte)GameFinderStatusUpdate.Failed);
                        }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                    }
                }

                foreach (var party in gameCandidate.AllParties())
                {
                    foreach (var player in party.Players)
                    {
                        var sectx = new SearchEndContext();
                        sectx.GameFinderId = this._scene.Id;
                        sectx.Party = party;
                        sectx.PassesCount = party.PastPasses;
                        sectx.Reason = SearchEndReason.Succeeded;
                        await handlers().RunEventHandler(h => h.OnEnd(sectx), ex => { });
                    }
                    var state = waitingClients[party];
                    state.Tcs.TrySetResult(null);
                }
            }
            catch (Exception)
            {
                await BroadcastToPlayers(gameCandidate, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Failed));
                throw;
            }
        }

        private ConcurrentDictionary<string, GameReadyCheck> _pendingReadyChecks = new ConcurrentDictionary<string, GameReadyCheck>();

        private GameReadyCheck CreateReadyCheck(IGameCandidate game)
        {
            var readyCheck = new GameReadyCheck(_data.readyCheckTimeout, () => CloseReadyCheck(game.Id), game);

            _pendingReadyChecks.TryAdd(game.Id, readyCheck);
            return readyCheck;
        }

        private void CloseReadyCheck(string id)
        {
            _pendingReadyChecks.TryRemove(id, out _);
        }

        private GameReadyCheck GetReadyCheck(IScenePeerClient peer)
        {
            if (_data.peersToGroup.TryGetValue(peer.SessionId, out var g))
            {
                var gameFinderRq = _data.waitingGroups[g];
                var gameCandidate = _data.waitingGroups[g].Candidate;
                if (gameCandidate == null)
                {
                    return null;
                }
                return GetReadyCheck(gameCandidate.Id);
            }
            return null;
        }

        private GameReadyCheck GetReadyCheck(string gameId)
        {
            GameReadyCheck check;
            if (_pendingReadyChecks.TryGetValue(gameId, out check))
            {
                return check;
            }
            else
            {
                return null;
            }
        }

        public async Task ResolveReadyRequest(Packet<IScenePeerClient> packet)
        {
            User user = null;
            using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                user = await sessions.GetUser(packet.Connection);
            }

            if (user == null)//User not authenticated
            {
                return;
            }

            var accepts = packet.Stream.ReadByte() > 0;

            var check = GetReadyCheck(packet.Connection);
            if (check == null)
            {
                return;
            }
            if (!check.ContainsPlayer(user.Id))
            {
                return;
            }

            check.ResolvePlayer(user.Id, accepts);
        }

        public Task CancelGame(Packet<IScenePeerClient> packet)
        {
            return CancelGame(packet.Connection, true);
        }

        public Task CancelGame(IScenePeerClient peer, bool requestedByPlayer)
        {
            Party party;
            if (!_data.peersToGroup.TryGetValue(peer.SessionId, out party))
            {
                return Task.CompletedTask;
            }

            return Cancel(party, requestedByPlayer);
        }

        public async Task CancelAll()
        {
            var tasks = new List<Task>();
            foreach (var party in _data.peersToGroup.Values.ToArray())
            {
                tasks.Add(Cancel(party, false));
            }
            await Task.WhenAll(tasks);
        }

        public Task Cancel(Party party, bool requestedByPlayer)
        {
            GameFinderRequestState mmrs;
            if (!_data.waitingGroups.TryGetValue(party, out mmrs))
            {
                return Task.CompletedTask;
            }

            mmrs.Tcs.TrySetCanceled();

            var sectx = new SearchEndContext();
            sectx.GameFinderId = this._scene.Id;
            sectx.Party = party;
            sectx.PassesCount = party.PastPasses;
            sectx.Reason = requestedByPlayer ? SearchEndReason.Canceled : SearchEndReason.Disconnected;
            return handlers().RunEventHandler(h => h.OnEnd(sectx), ex => { });
        }

        private Task<IScenePeerClient> GetPlayer(Player member)
        {
            using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                return sessions.GetPeer(member.UserId);
            }
        }

        private async Task<IEnumerable<IScenePeerClient>> GetPlayers(Party party)
        {
            return await Task.WhenAll(party.Players.Values.Select(GetPlayer));
        }

        private async Task<IEnumerable<IScenePeerClient>> GetPlayers(params Party[] parties)
        {
            return await Task.WhenAll(parties.SelectMany(g => g.Players.Values).Select(GetPlayer));
        }

        private Task BroadcastToPlayers(IGameCandidate game, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            return BroadcastToPlayers(game.Teams.SelectMany(t => t.Parties), route, writer);
        }

        private Task BroadcastToPlayers(Party party, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            return BroadcastToPlayers(new Party[] { party }, route, writer);
        }

        private async Task BroadcastToPlayers(IEnumerable<Party> parties, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            var peers = await GetPlayers(parties.ToArray());
            foreach (var party in peers.Where(p => p != null).GroupBy(p => p.Serializer()))
            {
                await _scene.Send(new MatchArrayFilter(party), route, s => writer(s, party.Key), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
            }
        }

        public Dictionary<string, int> GetMetrics() => _gameFinder.GetMetrics();

        //private class GameFinderContext : IGameFinderContext
        //{
        //    private TaskCompletionSource<bool> _tcs;

        //    public GameFinderContext(RequestContext<IScenePeerClient> request, TaskCompletionSource<bool> tcs, Group data)
        //    {
        //        _tcs = tcs;
        //        Request = request;
        //        Group = data;
        //        CreationTimeUTC = DateTime.UtcNow;
        //    }

        //    public DateTime CreationTimeUTC { get; }

        //    public Group Group { get; set; }


        //    public bool Rejected { get; private set; }
        //    public object RejectionData { get; private set; }

        //    /// <summary>
        //    /// Write the data sent to all the player when gameFinder completes (success or failure)
        //    /// </summary>
        //    public Action<System.IO.Stream, ISerializer> ResolutionWriter { get; set; }
        //    public bool MatchFound { get; private set; }
        //    public object MatchFoundData { get; private set; }


        //    public RequestContext<IScenePeerClient> Request { get; }

        //    public void Fail(object failureData)
        //    {

        //        if (IsResolved)
        //        {
        //            throw new InvalidOperationException("This gameFinder context has already been resolved.");
        //        }
        //        Rejected = true;
        //        RejectionData = failureData;
        //        _tcs.SetResult(false);
        //    }

        //    public void Success(object successData)
        //    {
        //        if (IsResolved)
        //        {
        //            throw new InvalidOperationException("This gameFinder context has already been resolved.");
        //        }
        //        MatchFound = true;
        //        MatchFoundData = successData;
        //        _tcs.SetResult(true);
        //    }

        //    public bool IsResolved
        //    {
        //        get
        //        {
        //            return MatchFound || Rejected;
        //        }
        //    }
        //}

        private class GameResolverContext : IGameResolverContext
        {
            public GameResolverContext(Game game)
            {
                Game = game;
            }

            public Game Game { get; }

            public Func<IGameFinderResolutionWriterContext, Task> ResolutionAction { get; set; }

            private string? _gameSceneId;

            public string GameSceneId { 
                get
                {
                    if(_gameSceneId!=null)
                    {
                        return _gameSceneId;
                    }
                    else
                    {
                        return Game.Id;
                    }

                }
                set
                {
                    _gameSceneId = value;
                }
            }
        }

        private class GameFinderResolutionWriterContext : IGameFinderResolutionWriterContext
        {
            private readonly Stream _stream;

            public GameFinderResolutionWriterContext(ISerializer serializer, Stream stream, IScenePeerClient peer)
            {
                Peer = peer;
                Serializer = serializer;
                _stream = stream;
            }

            public IScenePeerClient Peer { get; }
            public ISerializer Serializer { get; }

            public void WriteObjectToStream<T>(T data)
            {
                Serializer.Serialize(data, _stream);
            }

            public void WriteToStream(Action<Stream> writer)
            {
                writer(_stream);
            }
        }

        private static bool ClientSupportsV3Token(IScenePeerClient client)
        {
            if (client.Metadata.TryGetValue(GameFinderPlugin.ProtocolVersionKey, out var versionString))
            {
                if (long.TryParse(versionString, out long version) && version >= 2020_01_10_1)
                {
                    return true;
                }
            }
            return false;
        }

        async Task IGameFinderService.OpenGameSession(JObject data, RequestContext<IScenePeer> request)
        {
            var session = new OpenGameSession(data, request);

            bool added = _data.openGameSessions.TryAdd(session.SceneId, session);
            if (!added)
            {
                throw new InvalidOperationException("Game session is already opened");
            }

            _logger.Log(LogLevel.Trace, $"{LOG_CATEGORY}.OpenGameSession", "Opened a game session", new { session.SceneId, session.Data });

            try
            {
                request.CancellationToken.Register(() =>
                {
                    session.Close();
                    if (!IsRunning)
                    {
                        session.Complete();
                    }
                });

                await session.Tcs.Task;
            }
            finally
            {
                _data.openGameSessions.TryRemove(session.SceneId, out _);
                _logger.Log(LogLevel.Trace, $"{LOG_CATEGORY}.OpenGameSession", "Closed a game session", new { session.SceneId, session.Data });
            }
        }
    }
}
