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
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Context of a Gamefinding pass.
    /// </summary>
    public class GameFinderContext
    {
        private readonly IGameFinderService service;

        internal GameFinderContext(IGameFinderService service)
        {
            this.service = service;
        }

        /// <summary>
        /// Parties in the queue.
        /// </summary>
        public List<Party> WaitingParties { get; set; } = new List<Party>();

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
        /// <seealso cref="GameFinderController.OpenGameSession(JObject, IS2SRequestContext)"/>
        public List<OpenGameSession> OpenGameSessions { get; } = new List<OpenGameSession>();

        /// <summary>
        /// Check if each candidate is compatible with the others in its own collection.
        /// </summary>
        /// <param name="candidates">Collection of candidates.</param>
        /// <returns></returns>
        public IAsyncEnumerable<bool> AreCompatibleAsync(IEnumerable<Parties> candidates)
        {
            return service.AreCompatibleAsync(candidates);
        }

        /// <summary>
        /// Check if parties are compatible with the others.
        /// </summary>
        /// <param name="candidate">Candidates.</param>
        /// <returns></returns>
        public ValueTask<bool> AreCompatibleAsync(Parties candidate)
        {
            return AreCompatibleAsync(Enumerable.Repeat(candidate, 1)).FirstAsync();
        }
    }

    public readonly struct Parties
    {
        public Parties(IEnumerable<Party> parties)
        {
            Value = parties;
        }

        public IEnumerable<Party> Value { get; }
    }

    public struct FindGameResult
    {
        public bool Success { get; set; }
        public string ErrorMsg { get; set; }
    }

    internal class GameFinderService : IGameFinderService, IConfigurationChangedEventHandler
    {
        private const string UPDATE_NOTIFICATION_ROUTE = "gamefinder.update";
        private const string UPDATE_READYCHECK_ROUTE = "gamefinder.ready.update";
        private const string UPDATE_FINDGAME_REQUEST_PARAMS_ROUTE = "gamefinder.parameters.update";
        private const string LOG_CATEGORY = "GameFinderService";

        public const long ProtocolVersion = 2020_01_10_1;

        private ISceneHost _scene;
        private readonly IAnalyticsService _analytics;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly ISerializer _serializer;
        private readonly GameFinderData _data;

        // GameFinder Configuration
        public bool IsRunning { get => _data.IsRunning; private set => _data.IsRunning = value; }

        public GameFinderService(ISceneHost scene,
            IAnalyticsService analytics,
            IEnvironment env,
            ILogger logger,
            IConfiguration configuration,
            ISerializer serializer,
            GameFinderData data)
        {
            _analytics = analytics;
            _logger = logger;
            _configuration = configuration;
            _serializer = serializer;
            _data = data;
            _scene = scene;
            _data.kind = _scene.Metadata[GameFinderPlugin.METADATA_KEY];
            env.ActiveDeploymentChanged += Env_ActiveDeploymentChanged;

            ApplyConfig();
        }

        private void Env_ActiveDeploymentChanged(object? sender, ActiveDeploymentChangedEventArgs e)
        {
            if (!e.IsActive)
            {
                _data.acceptRequests = false;
                _ = CancelAll();
            }
        }

        private void ApplyConfig()
        {
            dynamic config = _configuration.Settings;
            if (_data.kind == null || config == null)
            {
                _logger.Log(LogLevel.Error, LOG_CATEGORY, "GameFinder service can't find gameFinder kind or server application config", new { gameFinderKind = _data.kind });
                return;
            }

            gameFinderConfigs = (JObject?)config?.gamefinder?.configs;
            dynamic? specificConfig = gameFinderConfigs?.GetValue(_data.kind);

            _data.interval = TimeSpan.FromSeconds((double)(specificConfig?.interval ?? 1));
            _data.isReadyCheckEnabled = (bool?)specificConfig?.readyCheck?.enabled ?? false;
            _data.readyCheckTimeout = (int)(specificConfig?.readyCheck?.timeout ?? 1000);
        }

        public async Task<FindGameResult> FindGame(Party party, CancellationToken ct)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                if (!_data.acceptRequests)
                {
                    return new FindGameResult { Success = false, ErrorMsg = "gamefinder.disabled?reason=deploymentNotActive" };
                }

                PlayerPeer[]? peersInGroup = null;
                await using (var scope = _scene.DependencyResolver.CreateChild(Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                {
                    var sessions = scope.Resolve<IUserSessions>();
                    peersInGroup = party.Players.Select(player =>
                    {
                        //var peer =_scene.RemotePeers.FirstOrDefault(p => p.SessionId.ToString() == player.Value.SessionId);
                        
                        //if (peer == null)
                        //{
                        //    throw new ClientException($"'{player.Value.UserId} is not connected to the gamefinder '{_scene.Id}'.");
                        //}
                        return new PlayerPeer { SessionId = SessionId.From(player.Value.SessionId), Player = player.Value };
                    }).ToArray();
                }
                var state = new GameFinderRequestState(party);

                try
                {
                    //foreach (var p in peersInGroup)
                    //{
                    //    if (p.Peer == null)
                    //    {
                    //        return new FindGameResult { Success = false, ErrorMsg = $"'{p.Player.UserId} has disconnected." };
                    //    }
                    //    //If player already waiting just replace infos instead of failing
                    //    //if (_data.peersToGroup.ContainsKey(p.Peer.Id))
                    //    //{
                    //    //    throw new ClientException($"'{p.Player.UserId} is already waiting for a game.");
                    //    //}
                    //}

                    _data.waitingParties[party] = state;
                    foreach (var p in peersInGroup)
                    {
                        _data.peersToGroup[p.SessionId] = party;
                    }

                    ct.Register(() =>
                    {
                        state.Tcs.TrySetCanceled();
                    });

                    var memStream = new MemoryStream();
                    //requestS2S.InputStream.Seek(0, SeekOrigin.Begin);
                    //requestS2S.InputStream.CopyTo(memStream);
                    //await BroadcastToPlayers(party, UPDATE_FINDGAME_REQUEST_PARAMS_ROUTE, (s, sz) =>
                    //{
                    //    memStream.Seek(0, System.IO.SeekOrigin.Begin);
                    //    memStream.CopyTo(s);
                    //});
                    await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                    {
                        s.WriteByte((byte)GameFinderStatusUpdate.SearchStart);

                    }, ct);
                    state.State = RequestState.Ready;
                }
                catch (Exception ex)
                {
                    state.Tcs.SetException(ex);
                    _logger.Log(LogLevel.Error, "gamefinder", $"Matchmaking failed : {ex}", ex);
                    _analytics.Push("gameFinder", "end", JObject.FromObject(new { partySize = party.Players.Count, duration = (DateTime.UtcNow - startTime).TotalMilliseconds, type = "failed" }));
                    await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Failed), ct);
                }

                try
                {
                    await state.Tcs.Task;
                    _analytics.Push("gameFinder", "end", JObject.FromObject(new { partySize = party.Players.Count, duration = (DateTime.UtcNow - startTime).TotalMilliseconds, type = "success" }));

                }
                catch (TaskCanceledException)
                {
                    _analytics.Push("gameFinder", "end", JObject.FromObject(new { partySize = party.Players.Count , duration = (DateTime.UtcNow - startTime).TotalMilliseconds, type = "cancelled" }));
                    await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Cancelled), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    return new FindGameResult { Success = false, ErrorMsg = ex.Message };
                }
                finally //Always remove party from list.
                {
                    foreach (var p in peersInGroup)
                    {
                        if (p?.SessionId != null)
                        {
                            _data.peersToGroup.TryRemove(p.SessionId, out _);
                        }
                    }

                    if (_data.waitingParties.TryRemove(party, out var group) && group.Candidate != null)
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
               return new FindGameResult { Success = true };
            }
            catch (Exception ex)
            {
                return new FindGameResult { Success = false, ErrorMsg = ex.Message };
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
                    try
                    {
                        watch.Restart();
                        await this.FindGamesOnce(ct);
                        watch.Stop();

                        var playersNum = this._data.waitingParties.Where(kvp => kvp.Value.State == RequestState.Ready).Sum(kvp => kvp.Key.Players.Count);
                        var groupsNum = this._data.waitingParties.Where(kvp => kvp.Value.State == RequestState.Ready).Count();
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
                }
                catch { }
                await Task.Delay(this._data.interval);
            }
            IsRunning = false;
        }

        private async Task FindGamesOnce(CancellationToken cancellationToken)
        {
            var waitingParties = _data.waitingParties.Where(kvp => kvp.Value.State == RequestState.Ready).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            try
            {
                await using (var scope = _scene.CreateRequestScope())
                {
                    foreach (var value in waitingParties.Values)
                    {
                        value.State = RequestState.Searching;
                        value.Candidate = null;
                    }

                    GameFinderContext mmCtx = new GameFinderContext(this);
                    mmCtx.WaitingParties.AddRange(waitingParties.Keys);
                    mmCtx.OpenGameSessions.AddRange(_data.openGameSessions.Values.Where(ogs => ogs.IsOpen));

                    var gameFinder = scope.Resolve<IGameFinderAlgorithm>();
                    var resolver = scope.Resolve<IGameFinderResolver>();

                    dynamic? specificConfig = gameFinderConfigs?.GetValue(_data.kind);
                    gameFinder.RefreshConfig(_data.kind, specificConfig);
                    resolver.RefreshConfig(_data.kind, specificConfig);

                    var games = await gameFinder.FindGames(mmCtx);

                    _analytics.Push("gameFinder", "pass", JObject.FromObject(new
                    {
                        type = _data.kind,
                        playersWaiting = _data.waitingParties.SelectMany(kvp => kvp.Key.Players).Count(),
                        parties = _data.waitingParties.Count(),
                        customData = gameFinder.ComputeDataAnalytics(mmCtx),
                        openGameSessions = _data.openGameSessions.Count
                    }));

                    foreach ((Party party, string reason) in mmCtx.FailedClients)
                    {
                        var client = waitingParties[party];
                        client.State = RequestState.Rejected;
                        // If this party is an S2S party, the exception will be forwarded to the S2S caller which is responsible for handling it
                        client.Tcs.TrySetException(new ClientException(reason));
                        // Remove games that contain a rejected party
                        games.Games.RemoveAll(m => m.AllParties().Contains(party));
                    }

                    if (games.Games.Any() || games.GameSessionTickets.Any())
                    {
                        //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Prepare resolutions {waitingParties.Count} players for {matches.Matches.Count} matches.", new { waitingCount = waitingParties.Count });
                        await resolver.PrepareGameResolution(games);
                    }

                    foreach (var game in games.Games)
                    {
                        foreach (var party in game.Teams.SelectMany(t => t.Parties)) //Set game found to prevent players from being gameed again
                        {
                            var state = waitingParties[party];
                            state.State = RequestState.Found;
                            state.Candidate = game;
                        }

                        //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Resolve game for {waitingParties.Count} players", new { waitingCount = waitingParties.Count, currentGame = game });
                        _ = ResolveGameFound(game, waitingParties, resolver, cancellationToken); // Resolve game, but don't wait for completion.
                                                                                                 //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Resolve complete game for {waitingParties.Count} players", new { waitingCount = waitingParties.Count, currentGame = game });
                    }

                    foreach (var ticket in games.GameSessionTickets)
                    {
                        foreach (var party in ticket.Teams.SelectMany(t => t.Parties))
                        {
                            var state = waitingParties[party];
                            state.State = RequestState.Found;
                            state.Candidate = ticket;
                        }

                        await ticket.GameSession.RegisterTeams(ticket.Teams);
                        _logger.Log(LogLevel.Trace, $"{LOG_CATEGORY}.FindGamesOnce", $"Registered {ticket.Teams.Count} teams for the open game session {ticket.Id}", new { GameSessionId = ticket.Id, ticket.Teams, ticket.GameSession.Data });

                        _ = ResolveGameFound(ticket, waitingParties, resolver, cancellationToken);
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
            }
            finally
            {
                foreach (var value in waitingParties.Values.Where(v => v.State == RequestState.Searching))
                {
                    value.State = RequestState.Ready;
                    value.Party.PastPasses++;
                }
            }
        }

        private async Task ResolveGameFound(IGameCandidate gameCandidate, Dictionary<Party, GameFinderRequestState> waitingParties, IGameFinderResolver resolver, CancellationToken cancellationToken)
        {
            try
            {
                Func<IGameFinderResolutionWriterContext, Task>? resolutionAction = null;
                string? gameSceneId = null;
                // I do not use 'if (gameCandidate is Game game)' so that 'game' does not leak in the outer scope
                if (gameCandidate is NewGame)
                {
                    var game = (NewGame)gameCandidate;
                    var resolverCtx = new GameResolverContext(game, _data.kind);
                    await resolver.ResolveGame(resolverCtx);
                    resolutionAction = resolverCtx.ResolutionAction;
                    gameSceneId = resolverCtx.GameSceneId;

                    var ctx = new GameStartedContext();
                    ctx.GameFinderId = this._scene.Id;
                    ctx.Game = game;

                    await RunEventHandlerInRequestScope<IGameFinderEventHandler>(_scene, h => h.OnGameStarted(ctx), ex => _logger.Log(LogLevel.Error, LOG_CATEGORY, "an error occured while running OnGameStarted event handler.", ex));
                }
                else if (gameCandidate is ExistingGame existingGame)
                {
                    var ctx = new JoinExistingGameContext(existingGame, _data.kind);
                    await resolver.ResolveJoinOpenGame(ctx);
                    resolutionAction = ctx.ResolutionAction;
                }
                else 
                {
                    gameSceneId = gameCandidate.Id;
                    resolutionAction = _ => Task.CompletedTask;
                }

                if (_data.isReadyCheckEnabled)
                {
                    await BroadcastToPlayers(gameCandidate, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                    {
                        s.WriteByte((byte)GameFinderStatusUpdate.WaitingPlayersReady);
                    }, cancellationToken);

                    using (var gameReadyCheckState = CreateReadyCheck(gameCandidate))
                    {
                        gameReadyCheckState.StateChanged += update =>
                        {
                            BroadcastToPlayers(gameCandidate, UPDATE_READYCHECK_ROUTE, (s, sz) =>
                            {
                                sz.Serialize(update, s);
                            }, cancellationToken);
                        };
                        var result = await gameReadyCheckState.WhenCompleteAsync();

                        if (!result.Success)
                        {
                            foreach (var party in result.UnreadyGroups)//Cancel gameFinder for timeouted parties
                            {
                                if (_data.waitingParties.TryGetValue(party, out var mrs))
                                {
                                    mrs.Tcs.TrySetCanceled();
                                }
                            }
                            foreach (var party in result.ReadyGroups)//Put ready parties back in queue.
                            {
                                if (_data.waitingParties.TryGetValue(party, out var mrs))
                                {
                                    mrs.State = RequestState.Ready;
                                    await BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                                    {
                                        s.WriteByte((byte)GameFinderStatusUpdate.SearchStart);
                                    }, cancellationToken);
                                }
                            }
                            return; //stop here
                        }
                    }
                }

                foreach (var player in GetPlayers(gameCandidate.AllParties(), cancellationToken))
                {
                    try
                    {
                        using (var stream = new MemoryStream())
                        {
                            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == player);
                            var writerContext = new GameFinderResolutionWriterContext(_serializer, stream, peer);
                            // Write the connection token first, if a scene was created by the resolver, or if joining an existing session
                            if (!string.IsNullOrEmpty(gameSceneId))
                            {
                                await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                                {
                                    var gameSessions = scope.Resolve<IGameSessions>();
                                    var token = await gameSessions.CreateConnectionToken(gameSceneId, player, TokenVersion.V3);
                                   
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
                            await _scene.Send(new MatchPeerFilter(player), UPDATE_NOTIFICATION_ROUTE, s =>
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
                        await _scene.Send(new MatchPeerFilter(player), UPDATE_NOTIFICATION_ROUTE, s =>
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
                        await RunEventHandlerInRequestScope<IGameFinderEventHandler>(_scene, h => h.OnEnd(sectx), ex => _logger.Log(LogLevel.Error, LOG_CATEGORY, "an error occured while running OnEnd event handler.", ex));
                    }
                    var state = waitingParties[party];
                    state.Tcs.TrySetResult(null);
                }
            }
            catch (Exception)
            {
                await BroadcastToPlayers(gameCandidate, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)GameFinderStatusUpdate.Failed), cancellationToken);
                throw;
            }
        }

        private ConcurrentDictionary<string, GameReadyCheck> _pendingReadyChecks = new ConcurrentDictionary<string, GameReadyCheck>();
        private JObject? gameFinderConfigs;

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

        private GameReadyCheck? GetReadyCheck(IScenePeerClient peer)
        {
            if (_data.peersToGroup.TryGetValue(peer.SessionId, out var g))
            {
                var gameFinderRq = _data.waitingParties[g];
                var gameCandidate = _data.waitingParties[g].Candidate;
                if (gameCandidate == null)
                {
                    return null;
                }
                return GetReadyCheck(gameCandidate.Id);
            }
            return null;
        }

        private GameReadyCheck? GetReadyCheck(string gameId)
        {

            if (_pendingReadyChecks.TryGetValue(gameId, out var check))
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
            User? user = null;
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                user = await sessions.GetUser(packet.Connection, CancellationToken.None);
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


        public async Task CancelGame(IScenePeerClient peer, bool requestedByPlayer)
        {

            if (!_data.peersToGroup.TryGetValue(peer.SessionId, out var party))
            {
                await _scene.Send(new MatchPeerFilter(peer), UPDATE_NOTIFICATION_ROUTE, s => s.WriteByte((byte)GameFinderStatusUpdate.Cancelled), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);

            }
            else
            {

                await Cancel(party, requestedByPlayer);
            }
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

            if (!_data.waitingParties.TryGetValue(party, out var mmrs))
            {
                return Task.CompletedTask;
            }

            mmrs.Tcs.TrySetCanceled();

            var sectx = new SearchEndContext();
            sectx.GameFinderId = this._scene.Id;
            sectx.Party = party;
            sectx.PassesCount = party.PastPasses;
            sectx.Reason = requestedByPlayer ? SearchEndReason.Canceled : SearchEndReason.Disconnected;
            return RunEventHandlerInRequestScope<IGameFinderEventHandler>(_scene, h => h.OnEnd(sectx), ex => _logger.Log(LogLevel.Error, LOG_CATEGORY, "an error occured while running OnEnd event handler.", ex));
        }

        private SessionId GetPlayer(Player member, CancellationToken cancellationToken)
        {
            return SessionId.From(member.SessionId);
           
        }

        private  IEnumerable<SessionId> GetPlayers(Party party, CancellationToken cancellationToken)
        {

            return party.Players.Values.Select(p => GetPlayer(p, cancellationToken));
        }

        private IEnumerable<SessionId> GetPlayers(IEnumerable<Party> parties, CancellationToken cancellationToken)
        {
            return parties.SelectMany(g => g.Players.Values).Select(p => GetPlayer(p, cancellationToken));

        
        }

        private Task BroadcastToPlayers(IGameCandidate game, string route, Action<System.IO.Stream, ISerializer> writer, CancellationToken cancellationToken)
        {
            return BroadcastToPlayers(game.Teams.SelectMany(t => t.Parties), route, writer, cancellationToken);
        }

        private Task BroadcastToPlayers(Party party, string route, Action<System.IO.Stream, ISerializer> writer, CancellationToken cancellationToken)
        {
            return BroadcastToPlayers(Enumerable.Repeat(party, 1), route, writer, cancellationToken);
        }

        private async Task BroadcastToPlayers(IEnumerable<Party> parties, string route, Action<System.IO.Stream, ISerializer> writer, CancellationToken cancellationToken)
        {
            var peers = GetPlayers(parties, cancellationToken);
           
            await _scene.Send(new MatchArrayFilter(peers), route, s => writer(s, _serializer), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
            
        }

        public async ValueTask<Dictionary<string, int>> GetMetrics()
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                return scope.Resolve<IGameFinderAlgorithm>().GetMetrics();
            }

        }


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
            internal GameResolverContext(NewGame game, string gameFinderName)
            {
                Game = game;
                GameFinderName = gameFinderName;
            }

            public NewGame Game { get; }
            public string GameFinderName { get; }

            /// <summary>
            /// Sets an action executed during Game Resolution.
            /// </summary>
            public Func<IGameFinderResolutionWriterContext, Task> ResolutionAction { get; set; } = _ => Task.CompletedTask;

            private string? _gameSceneId;

            public string GameSceneId
            {
                get
                {
                    if (_gameSceneId != null)
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

        async IAsyncEnumerable<IEnumerable<Team>> IGameFinderService.OpenGameSession(JObject data, IS2SRequestContext request)
        {
            var subject = new Subject<IEnumerable<Team>>();
            var indexOfSharp = request.Origin.IndexOf('#');

            var origin = indexOfSharp < 0 ? request.Origin : request.Origin.Substring(0, indexOfSharp);
            if (indexOfSharp >= 0)
            {
            }

            var session = new OpenGameSession(origin, data, subject);

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

                await foreach (var item in subject.ToAsyncEnumerable().WithCancellation(request.CancellationToken))
                {
                    yield return item;
                }
            }
            finally
            {
                _data.openGameSessions.TryRemove(session.SceneId, out _);
                _logger.Log(LogLevel.Trace, $"{LOG_CATEGORY}.OpenGameSession", "Closed a game session", new { session.SceneId, session.Data });
            }
        }

        public void OnConfigurationChanged()
        {
            ApplyConfig();
        }

        public async IAsyncEnumerable<bool> AreCompatibleAsync(IEnumerable<Parties> candidates)
        {
            var ctx = new AreCompatibleContext(candidates);

            await RunEventHandlerInRequestScope<IGameFinderEventHandler>(_scene, h => h.AreCompatibleAsync(ctx), ex => _logger.Log(LogLevel.Error, LOG_CATEGORY, "an error occured while running AreCompatible event handler.", ex));

            foreach (var result in ctx.Results)
            {
                yield return result;
            }
        }

        public async static Task RunEventHandlerInRequestScope<THandler>(ISceneHost scene, Func<THandler, Task> onRun, Action<Exception> onError) where THandler : class
        {
            await using (var scope = scene.CreateRequestScope())
            {
                await scope.ResolveAll<THandler>().RunEventHandler(onRun, onError);
            }
        }
    }
}
