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

using MessagePack;
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
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    public class GameFinderContext : IGameFinderContext
    {
        private readonly IGameFinderService service;
        private readonly IDependencyResolver _scope;
        internal GameFinderContext(IGameFinderService service, IDependencyResolver scope)
        {
            this.service = service;
            this._scope = scope;
        }

        ///<inheritdoc/>
        public IDependencyResolver Scope => _scope;

        /// <summary>
        /// Parties in the queue.
        /// </summary>
        public List<Party> WaitingParties { get; } = new List<Party>();

        /// <summary>
        /// List of parties which failed matchmaking during this pass.
        /// </summary>
        public List<(Party client, string reason)> FailedClients { get; } = new List<(Party client, string reason)>();

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



    /// <summary>
    /// Result of a game finding session
    /// </summary>
    [MessagePackObject]
    public struct FindGameResult
    {
        /// <summary>
        /// Was the operation successful
        /// </summary>
        [Key(0)]
        [MemberNotNullWhen(false, "ErrorMsg")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets an error message.
        /// </summary>
        [Key(1)]
        public string? ErrorMsg { get; set; }
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
                        return new PlayerPeer { SessionId = player.Value.SessionId, Player = player.Value };
                    }).ToArray();
                }

                if(!_data.TryAddParty(party, out var state))
                {
                    BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                    {
                        var span = s.GetSpan(1);
                        span[0] = (byte)GameFinderPlayerState.Failed;
                        s.Advance(1);
                    });
                    return new FindGameResult { Success = false, ErrorMsg = "AlreadySearching" };
                }

                using var registration = ct.Register(() =>
                {
                    state.SetCanceled();
                });

                try
                {

                   

                    BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                    {
                        var span = s.GetSpan(1);
                        span[0] = (byte)GameFinderPlayerState.Searching;
                        s.Advance(1);

                    });
                   
                }
                catch (Exception ex)
                {
                    if (_data.TryCompletePartyError(state.Party.PartyId, ex.Message))
                    {
                        _logger.Log(LogLevel.Error, "gamefinder", $"Matchmaking failed : {ex}", ex);
                        _analytics.Push("gameFinder", "end", JObject.FromObject(new { partySize = party.Players.Count, duration = (DateTime.UtcNow - startTime).TotalMilliseconds, type = "failed" }));
                        BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                        {
                            var span = s.GetSpan(1);
                            span[0] = (byte)GameFinderPlayerState.Failed;
                            s.Advance(1);
                        });
                    }
                }

                FindGameResult result;
                try
                {
                    result = await state.WhenCompleted;
                    _analytics.Push("gameFinder", "end", JObject.FromObject(new { partySize = party.Players.Count, duration = (DateTime.UtcNow - startTime).TotalMilliseconds, type = "success" }));

                }
                catch (TaskCanceledException)
                {
                    _analytics.Push("gameFinder", "end", JObject.FromObject(new { partySize = party.Players.Count, duration = (DateTime.UtcNow - startTime).TotalMilliseconds, type = "cancelled" }));
                    BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                    {
                        var span = s.GetSpan(1);
                        span[0] = (byte)GameFinderPlayerState.Canceled;
                        s.Advance(1);
                    });
                    return new FindGameResult { Success = false, ErrorMsg = "canceled" };
                }
                catch (Exception ex)
                {
                    return new FindGameResult { Success = false, ErrorMsg = ex.Message };
                }
               
                return result;
            }
            catch (Exception ex)
            {
                return new FindGameResult { Success = false, ErrorMsg = ex.Message };
            }
        }

        public CancellationToken GameFinderStoppedToken { get; private set; }
        public async Task Run(CancellationToken ct)
        {
            IsRunning = true;
            GameFinderStoppedToken = ct;
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
            var waitingParties = _data.WaitingParties.Where(kvp => kvp.Value.State == RequestState.Ready).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            try
            {
                await using (var scope = _scene.CreateRequestScope())
                {
                    foreach (var value in waitingParties.Values)
                    {
                        value.State = RequestState.Searching;
                        value.Candidate = null;
                    }

                    GameFinderContext mmCtx = new GameFinderContext(this, scope);
                    mmCtx.WaitingParties.AddRange(waitingParties.Values.Select(s => s.Party));

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var gameFinder = scope.Resolve<IGameFinderAlgorithm>();
                    var resolver = scope.Resolve<IGameFinderResolver>();

                    dynamic? specificConfig = gameFinderConfigs?.GetValue(_data.kind);
                    gameFinder.RefreshConfig(_data.kind, specificConfig);
                    resolver.RefreshConfig(_data.kind, specificConfig);

                    var games = await gameFinder.FindGames(mmCtx);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _analytics.Push("gameFinder", "pass", JObject.FromObject(new
                    {
                        type = _data.kind,
                        playersWaiting = waitingParties.SelectMany(kvp => kvp.Value.Party.Players).Count(),
                        customData = gameFinder.ComputeDataAnalytics(mmCtx)
                    }));

                    foreach (var (party, reason) in mmCtx.FailedClients)
                    {
                        var client = waitingParties[party.PartyId];
                        if(_data.TryCompletePartyError(party.PartyId,reason))
                        {
                            client.State = RequestState.Rejected;
                            NotifyError(party, reason);
                        }

                        // Cancel games that contain a rejected party
                        games.Games.RemoveAll(m => m.Parties.Any(p => p.Id == party.PartyId));

                    }

                    if (games.Games.Any())
                    {
                        await resolver.PrepareGameResolution(games);
                    }

                    foreach (var game in games.Games)
                    {
                        foreach (var party in game.Parties) //Set game found to prevent players from being gameed again
                        {
                            var state = waitingParties[party.Id];
                            state.State = RequestState.Found;
                            state.Candidate = game;
                        }

                        //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Resolve game for {waitingParties.Count} players", new { waitingCount = waitingParties.Count, currentGame = game });
                        await ResolveGameFound(game, waitingParties, resolver, cancellationToken); // Resolve game, but don't wait for completion.
                                                                                                   //_logger.Log(LogLevel.Debug, $"{LOG_CATEGORY}.FindGamesOnce", $"Resolve complete game for {waitingParties.Count} players", new { waitingCount = waitingParties.Count, currentGame = game });
                    }

                }
            }
            catch (ObjectDisposedException) // The scene was destroyed, ignore the error.
            {

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

        private async Task ResolveGameFound(IGameCandidate gameCandidate, Dictionary<string, GameFinderRequestState> waitingParties, IGameFinderResolver resolver, CancellationToken cancellationToken)
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
                    gameSceneId = existingGame.Id;

                    resolutionAction = ctx.ResolutionAction;
                }
                else
                {

                    resolutionAction = _ => Task.CompletedTask;
                }


                foreach (var partySummary in gameCandidate.Parties)
                {
                    if (waitingParties.TryGetValue(partySummary.Id, out var state))
                    {
                        var party = state.Party;
                        foreach (var player in party.Players.Values.Select(member => GetPlayer(member)))
                        {
                            try
                            {
                                var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == player);
                                if (peer != null)
                                {
                                    using (var stream = new MemoryStream())
                                    {


                                        var writerContext = new GameFinderResolutionWriterContext(_serializer, stream, peer);
                                        // Write the connection token first, if a scene was created by the resolver, or if joining an existing session
                                        if (!string.IsNullOrEmpty(gameSceneId))
                                        {
                                            await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                                            {
                                                var gameSessions = scope.Resolve<IGameSessions>();
                                                var pId = System.Text.Encoding.ASCII.GetBytes(party.PartyId);
                                                var token = await gameSessions.CreateConnectionToken(gameSceneId, player, pId, "stormancer/partyId", TokenVersion.V3);

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
                                        _scene.Send(new MatchPeerFilter(player), UPDATE_NOTIFICATION_ROUTE, static (System.Buffers.IBufferWriter<byte> s, MemoryStream stream) =>
                                        {
                                            var span = s.GetSpan(1 + (int)stream.Length);
                                            span[0] = (byte)GameFinderPlayerState.Connecting;


                                            stream.Seek(0, SeekOrigin.Begin);
                                            stream.Read(span.Slice(1));
                                            s.Advance(1 + (int)stream.Length);
                                        }
                                        , PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, stream);
                                    }
                                }
                            }
                            catch (ClientException ex)
                            {
                                _scene.Send(new MatchPeerFilter(player), UPDATE_NOTIFICATION_ROUTE, static (s, tuple) =>
                                {
                                    var (ex, serializer) = tuple;
                                    var span = s.GetSpan(1);
                                    span[0] = (byte)GameFinderPlayerState.Failed;
                                    s.Advance(1);
                                    serializer.Serialize(ex.Message, s);
                                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (ex, _serializer));
                            }
                            catch (Exception ex)
                            {
                                _logger.Log(LogLevel.Error, "gamefinder", "An error occured while trying to resolve a game for a player", ex);
                                _scene.Send(new MatchPeerFilter(player), UPDATE_NOTIFICATION_ROUTE, static (s, _) =>
                                {
                                    var span = s.GetSpan(1);
                                    span[0] = (byte)GameFinderPlayerState.Failed;
                                    s.Advance(1);
                                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, false);
                            }
                        }
                    }
                }

                foreach (var partySummary in gameCandidate.Parties)
                {
                    if (waitingParties.TryGetValue(partySummary.Id, out var state))
                    {
                        var party = state.Party;
                        var sectx = new SearchEndContext
                        {
                            GameFinderId = this._scene.Id,
                            Party = state.Party,
                            PassesCount = party.PastPasses,
                            Reason = SearchEndReason.Succeeded
                        };
                        await RunEventHandlerInRequestScope<IGameFinderEventHandler>(_scene, h => h.OnEnd(sectx), ex => _logger.Log(LogLevel.Error, LOG_CATEGORY, "an error occured while running OnEnd event handler.", ex));

                        _data.TryCompletePartySuccess(party.PartyId);
                        waitingParties.Remove(party.PartyId);
                    }
                }
            }
            catch (Exception)
            {
                BroadcastToPlayers(gameCandidate, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                {
                    var span = s.GetSpan(1);
                    span[0] = (byte)GameFinderPlayerState.Failed;
                    s.Advance(1);
                });
                throw;
            }
        }


        private JObject? gameFinderConfigs;


        private void NotifyError(Party party, string error)
        {
            BroadcastToPlayers(party, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
            {
                var span = s.GetSpan(1);
                span[0] = (byte)GameFinderPlayerState.Failed;
                s.Advance(1);
                _serializer.Serialize(error, s);
            });

        }

        public async Task CancelGame(IScenePeerClient peer, bool requestedByPlayer)
        {

            if (_data.TryGetParty(peer.SessionId, out var party))
            {
                await Cancel(party, requestedByPlayer);

            }
            else
            {
                _scene.Send(new MatchPeerFilter(peer), UPDATE_NOTIFICATION_ROUTE, (s, i) =>
                {
                    var span = s.GetSpan(1);
                    span[0] = i;
                    s.Advance(1);
                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (byte)GameFinderPlayerState.Canceled);
               
            }
        }

        public async Task CancelAll()
        {
            var tasks = new List<Task>();
            foreach (var state in _data.WaitingParties.Values)
            {
                tasks.Add(Cancel(state.Party, false));
            }
            await Task.WhenAll(tasks);
        }

        public Task Cancel(Party party, bool requestedByPlayer)
        {

            if (!_data.TryCancelParty(party.PartyId))
            {
                return Task.CompletedTask;
            }

            var sectx = new SearchEndContext
            {
                GameFinderId = this._scene.Id,
                Party = party,
                PassesCount = party.PastPasses,
                Reason = requestedByPlayer ? SearchEndReason.Canceled : SearchEndReason.Disconnected
            };
            return RunEventHandlerInRequestScope<IGameFinderEventHandler>(_scene, h => h.OnEnd(sectx), ex => _logger.Log(LogLevel.Error, LOG_CATEGORY, "an error occured while running OnEnd event handler.", ex));
        }

        private SessionId GetPlayer(Player member)
        {
            return member.SessionId;

        }

        private IEnumerable<SessionId> GetPlayers(Party party)
        {

            return party.Players.Values.Select(p => GetPlayer(p));
        }

        private IEnumerable<SessionId> GetPlayers(IEnumerable<Party> parties)
        {
            return parties.SelectMany(g => g.Players.Values).Select(p => GetPlayer(p));


        }

        private void BroadcastToPlayers(IGameCandidate game, string route, Action<IBufferWriter<byte>, ISerializer> writer)
        {
            BroadcastToPlayers(game.Parties.Select(p => _data.TryGetStateForParty(p.Id, out var state) ? state.Party : null).WhereNotNull(), route, writer);
        }

        private void BroadcastToPlayers(Party party, string route, Action<IBufferWriter<byte>, ISerializer> writer)
        {
            BroadcastToPlayers(Enumerable.Repeat(party, 1), route, writer);
        }

        private void BroadcastToPlayers(IEnumerable<Party> parties, string route, Action<IBufferWriter<byte>, ISerializer> writer)
        {
            var peers = GetPlayers(parties);

            _scene.Send(new MatchArrayFilter(peers), route, static (s, tuple) =>
            {
                var (writer, serializer) = tuple;
                writer(s, serializer);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (writer, _serializer));

        }

        public async Task<JObject> GetStatus(bool isAdmin)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                return scope.Resolve<IGameFinderAlgorithm>().GetStatus(isAdmin);
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
