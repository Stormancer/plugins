﻿using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    /// <summary>
    /// State of the party merging process in the party scene.
    /// </summary>
    internal class PartyMergingState
    {
        public readonly object _syncRoot = new object();
        public readonly Dictionary<string, PartyMergingPartyState> _states = new Dictionary<string, PartyMergingPartyState>();

        public int LastPlayersCount { get; set; }
        public int LastPartiesCount { get; set; }

        public AnalyticsAccumulator<TimeSpan, double> AverageTimeInMerger { get; set; } = new AnalyticsAccumulator<TimeSpan, double>(256, (span) => 
        {

            TimeSpan result = TimeSpan.Zero;
            for(int i = 0; i < span.Length;i++)
            {
                result += span[i];
            }
            if (span.Length > 0)
            {
                return result.TotalSeconds / span.Length;
            }
            else
            {
                return 0;
            }
        });

        public PartyMergingState(ISceneHost scene)
        {
            scene.RunTask(async (ct) =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                var lastAnalytics = DateTime.UtcNow;
                while (!ct.IsCancellationRequested)
                {
                    await timer.WaitForNextTickAsync(ct);
                    await using var scope = scene.CreateRequestScope();
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(10000);
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
                        var merger = scope.Resolve<PartyMergingService>();
                        await merger.Merge(cts.Token);

                        if (DateTime.UtcNow > lastAnalytics + TimeSpan.FromMinutes(1))
                        {
                            lastAnalytics = DateTime.UtcNow;
                            var analytics = scope.Resolve<IAnalyticsService>();
                            var data = merger.GetAnalytics();
                            
                            analytics.Push("merger", merger.MergerId, JObject.FromObject(data));

                            if(data.LastPlayerCount>0)
                            {
                                await scene.KeepAlive(TimeSpan.FromMinutes(5));
                            }
                            
                        }
                      
                    }
                    catch (Exception ex)
                    {
                        if (PartyMergingConstants.TryGetMergerId(scene, out var mergerId))
                        {
                            scope.Resolve<ILogger>().Log(LogLevel.Error, "partyMerger", $"An error occurred while running the party merger {mergerId}.", ex);
                        }
                    }
                }
            });

        }
    }


    internal class PartyMergingPartyState : IDisposable
    {
        public PartyMergingPartyState(string partyId, CancellationToken cancellationToken)
        {
            PartyId = partyId;
            LinkCancellationToken(cancellationToken);
        }
        private readonly TaskCompletionSource<string?> _completedTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        private CancellationTokenSource _cts;
        private CancellationTokenRegistration _registration;
        private List<CancellationToken> _cancellationTokens = new List<CancellationToken>();
        public string PartyId { get; }
        public DateTime EnteredOn { get; } = DateTime.UtcNow;

        public bool IsCancellationRequested => _cts.IsCancellationRequested;

        public Dictionary<string, object> CacheStorage { get; } = new Dictionary<string, object>();

        public Task<string?> WhenCompletedAsync()
        {
            return _completedTcs.Task;
        }

        public void Complete(string? connectionToken)
        {
            _completedTcs.TrySetResult(connectionToken);
        }
        public void Cancel()
        {
            _completedTcs.TrySetCanceled();
        }
        public void Dispose()
        {
            Cancel();

            _registration.Unregister();
            _cts.Dispose();

        }

        [MemberNotNull("_cts")]
        internal void LinkCancellationToken(CancellationToken cancellationToken)
        {
            _cancellationTokens.Add(cancellationToken);
            if (_cts != null)
            {

                var currentCts = _cts;
                var currentRegistration = _registration;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokens.ToArray());

                _registration = _cts.Token.Register(() => Cancel());

                currentRegistration.Unregister();
                currentCts.Dispose();

            }
            else
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _registration = _cts.Token.Register(() => { _completedTcs.TrySetCanceled(); });
            }


        }
    }

    internal class PartyMergingService
    {

        private readonly ISceneHost _scene;
        private readonly PartyMergingState _state;
        private readonly IPartyMergingAlgorithm _algorithm;
        private readonly PartyProxy _parties;
        private readonly IPartyManagementService _partyManagement;

        public string MergerId => PartyMergingConstants.TryGetMergerId(_scene, out var mergerId) ? mergerId : "unknown";

        public PartyMergingService(ISceneHost scene, PartyMergingState state, IPartyMergingAlgorithm algorithm, PartyProxy parties, IPartyManagementService partyManagement)
        {
            _scene = scene;
            _state = state;
            _algorithm = algorithm;
            _parties = parties;
            _partyManagement = partyManagement;
        }

        public async Task<string?> StartMergeParty(string partyId, CancellationToken cancellationToken)
        {
            PartyMergingPartyState? state = null;
            try
            {
               
                lock (_state._syncRoot)
                {
                    if (_state._states.TryGetValue(partyId, out var currentState))
                    {
                        state = currentState;
                        if (cancellationToken.CanBeCanceled)
                        {
                            state.LinkCancellationToken(cancellationToken);
                        }
                    }
                    else
                    {
                        state = new PartyMergingPartyState(partyId, cancellationToken);
                        _state._states.Add(partyId, state);
                    }
                }

                using (state)
                {

                    return await state.WhenCompletedAsync();
                }

            }
            finally
            {
                lock (_state._syncRoot)
                {
                    _state._states.Remove(partyId);
                    if (state != null)
                    {
                        _state.AverageTimeInMerger.Add(DateTime.UtcNow - state.EnteredOn);
                    }
                }
            }
        }

        public void StopMergeParty(string partyId)
        {
            lock (_state._syncRoot)
            {
                if (_state._states.TryGetValue(partyId, out var state))
                {

                    state.Cancel();
                }
            }
        }


        public async Task Merge(CancellationToken cancellationToken)
        {
            IEnumerable<Task<Models.Party?>> tasks;
            lock (_state._syncRoot)
            {


                tasks = _state._states.Where(kvp => !kvp.Value.IsCancellationRequested).Select(async kvp =>
                {
                    try
                    {
                        var model = await _parties.GetModel(kvp.Key, cancellationToken);
                        model.CacheStorage = kvp.Value.CacheStorage;
                        return model;
                    }
                    catch (Exception)
                    {
                        kvp.Value.Cancel();
                        return null;
                    }
                });
            }

            var models = await Task.WhenAll(tasks);


            var ctx = new PartyMergingContext(models.WhereNotNull());
            _state.LastPlayersCount = ctx.WaitingParties.Sum(p => p.Players.Count);
            _state.LastPartiesCount = ctx.WaitingParties.Count();
            await _algorithm.Merge(ctx);



            foreach (var (cmd, mergeTask) in ctx.MergeCommands.Select(cmd => (cmd, MergeAsync(cmd.From, cmd.Into, cmd.CustomData, cancellationToken))))
            {
                try
                {

                    var partyTo = await mergeTask;

                    if (partyTo != null)
                    {

                        if (_algorithm.CanCompleteMerge(partyTo))
                        {
                            lock (_state._syncRoot)
                            {
                                if (_state._states.TryGetValue(partyTo.PartyId, out var state))
                                {
                                    state.Complete(null);
                                    _state._states.Remove(partyTo.PartyId);
                                }
                            }
                        }
                    }

                }
                catch (Exception)
                {
                    lock (_state._syncRoot)
                    {
                        if (_state._states.TryGetValue(cmd.Into.PartyId, out var state))
                        {
                            state.Cancel();
                            _state._states.Remove(cmd.Into.PartyId);
                        }
                    }
                }
            }
        }

        private async Task<Models.Party?> MergeAsync(Models.Party partyFrom, Models.Party partyTo, JObject customData, CancellationToken cancellationToken)
        {
            var result = await _partyManagement.CreateConnectionTokenFromPartyId(partyTo.PartyId, Memory<byte>.Empty, cancellationToken);

            if (customData == null)
            {
                customData = new JObject();
            }

            if (result.Success)
            {
                PartyMergingConstants.TryGetMergerId(_scene, out var mergerId);

                customData["merger"] = mergerId;
                var reservation = new PartyReservation { PartyMembers = partyFrom.Players.Values, CustomData = customData };
                await _parties.CreateReservation(partyTo.PartyId, reservation, cancellationToken);


                lock (_state._syncRoot)
                {
                    if (_state._states.TryGetValue(partyFrom.PartyId, out var state))
                    {
                        state.Complete(result.Value);

                        _state._states.Remove(partyFrom.PartyId);

                        foreach (var (key, value) in partyFrom.Players)
                        {
                            partyTo.Players.Add(key, value);
                        }

                    }
                    return partyTo;
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<JObject> GetStatusAsync(bool fromAdmin)
        {
           
            var json = JObject.FromObject(new
            {
                partiesCount = _state.LastPartiesCount,
                algorithm = _algorithm.GetType().Name,
                playersCount = _state.LastPlayersCount
            });
            json["details"] = await _algorithm.GetStatusAsync(fromAdmin);

            return json;
        }

        public MergerAnalytics GetAnalytics()
        {
            return new MergerAnalytics { AverageTimeInMerger = _state.AverageTimeInMerger.Result, LastPlayerCount = _state.LastPlayersCount, LastPartyCount = _state.LastPartiesCount, Custom =  _algorithm.GetAnalytics() };
        }
    }

    /// <summary>
    /// Analytics data about a merger.
    /// </summary>
    public class MergerAnalytics
    {
        /// <summary>
        /// Last number of players known
        /// </summary>
        public required int LastPlayerCount { get; init; }

        /// <summary>
        /// Last known number of parties in the merger.
        /// </summary>
        public required int LastPartyCount { get;init; }

        /// <summary>
        /// Average time passed in the merger for the last merging requests.
        /// </summary>
        public required double AverageTimeInMerger { get; init; }

        /// <summary>
        /// Custom data
        /// </summary>
        public required JObject Custom { get; init; }

    }
}
