using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.PartyFinder;
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    internal class PartyMergingState
    {
        public readonly object _syncRoot = new object();
        public readonly Dictionary<string, PartyMergingPartyState> _states = new Dictionary<string, PartyMergingPartyState>();

        public PartyMergingState(ISceneHost scene)
        {
            scene.RunTask(async (ct) =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

                while (!ct.IsCancellationRequested)
                {
                    await timer.WaitForNextTickAsync(ct);
                    await using var scope = scene.CreateRequestScope();
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(10000);
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
                        await scope.Resolve<PartyMergingService>().Merge(cts.Token);

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
        public void Dispose()
        {
            _completedTcs.TrySetException(new OperationCanceledException());

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

                _registration = _cts.Token.Register(() => { _completedTcs.TrySetCanceled(); });

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
            try
            {
                PartyMergingPartyState state;
                lock (_state._syncRoot)
                {
                    if (_state._states.TryGetValue(partyId, out var currentState))
                    {
                        state = currentState;
                        state.LinkCancellationToken(cancellationToken);
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
                }
            }
        }


        public async Task Merge(CancellationToken cancellationToken)
        {
            IEnumerable<Task<Models.Party>> tasks;
            lock (_state._syncRoot)
            {


                tasks = _state._states.Where(kvp => !kvp.Value.IsCancellationRequested).Select(async kvp =>
                {
                    var model = await _parties.GetModel(kvp.Key, cancellationToken);
                    model.CacheStorage = kvp.Value.CacheStorage;
                    return model;
                });
            }

            var models = await Task.WhenAll(tasks);


            var ctx = new PartyMergingContext(models);
            await _algorithm.Merge(ctx);


            var results = await Task.WhenAll(ctx.MergeCommands.Select(cmd => MergeAsync(cmd.From, cmd.Into, cmd.CustomData, cancellationToken)));


            foreach (var partyId in results.Distinct().WhereNotNull())
            {
                lock (_state._syncRoot)
                {
                    if (_state._states.TryGetValue(partyId, out var state))
                    {
                        state.Complete(null);
                        _state._states.Remove(partyId);
                    }
                }
            }
        }

        private async Task<string?> MergeAsync(Models.Party partyFrom, Models.Party partyTo, JObject customData, CancellationToken cancellationToken)
        {
            var result = await _partyManagement.CreateConnectionTokenFromPartyId(partyTo.PartyId, Memory<byte>.Empty, cancellationToken);



            if (result.Success)
            {
                var reservation = new PartyReservation { PartyMembers = partyFrom.Players.Values, CustomData = customData };
                await _parties.CreateReservation(partyTo.PartyId, reservation, cancellationToken);


                lock (_state._syncRoot)
                {
                    if (_state._states.TryGetValue(partyFrom.PartyId, out var state))
                    {
                        state.Complete(result.Value);

                        _state._states.Remove(partyFrom.PartyId);

                    }
                    return partyTo.PartyId;
                }
            }
            else
            {
                return null;
            }
        }
    }
}
