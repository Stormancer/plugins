using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Party;
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

    internal class PartyMergingService
    {
        private class PartyMergingState : IDisposable
        {
            public PartyMergingState(string partyId, CancellationToken cancellationToken)
            {
                PartyId = partyId;
                LinkCancellationToken(cancellationToken);
            }
            private readonly TaskCompletionSource<string?> _completedTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenSource _cts;

            public string PartyId { get; }

            public bool IsCancellationRequested => _cts.IsCancellationRequested;
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
                _cts.Dispose();
            }

            [MemberNotNull("_cts")]
            internal void LinkCancellationToken(CancellationToken cancellationToken)
            {
                if (_cts != null)
                {
                    _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                }
                else
                {
                    _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

            }
        }
        private readonly ISceneHost _scene;
        private readonly IPartyMergingAlgorithm _algorithm;
        private readonly PartyProxy _parties;
        private readonly IPartyManagementService _partyManagement;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, PartyMergingState> _states = new Dictionary<string, PartyMergingState>();

        public PartyMergingService(ISceneHost scene, IPartyMergingAlgorithm algorithm, PartyProxy parties, IPartyManagementService partyManagement)
        {
            _scene = scene;
            _algorithm = algorithm;
            _parties = parties;
            _partyManagement = partyManagement;
        }

        public async Task<string?> StartMergeParty(string partyId, CancellationToken cancellationToken)
        {
            try
            {
                PartyMergingState state;
                lock (_syncRoot)
                {
                    if (_states.TryGetValue(partyId, out var currentState))
                    {
                        state = currentState;
                        state.LinkCancellationToken(cancellationToken);
                    }
                    else
                    {
                        state = new PartyMergingState(partyId, cancellationToken);
                    }
                }

                using (state)
                {

                    return await state.WhenCompletedAsync();
                }

            }
            finally
            {
                lock (_syncRoot)
                {
                    _states.Remove(partyId);
                }
            }
        }


        public async Task Merge(CancellationToken cancellationToken)
        {
            IEnumerable<Task<Models.Party>> tasks;
            lock (_syncRoot)
            {


                tasks = _states.Where(kvp => !kvp.Value.IsCancellationRequested).Select(kvp => _parties.GetModel(kvp.Key, cancellationToken));
             }

            var models = await Task.WhenAll(tasks);


            var ctx = new PartyMergingContext(models);
            await _algorithm.Merge(ctx);


            var results = await Task.WhenAll(ctx.MergeCommands.Select(cmd => MergeAsync(cmd.Value.From, cmd.Value.Into, cmd.Value.CustomData, cancellationToken)));


            foreach(var partyId in results.Distinct().WhereNotNull())
            {
                if (_states.TryGetValue(partyId, out var state))
                {
                    state.Complete(null);
                }
            }
        }

        private async Task<string?> MergeAsync(Models.Party partyFrom, Models.Party partyTo, JObject customData,CancellationToken cancellationToken)
        {
            var result = await _partyManagement.CreateConnectionTokenFromPartyId(partyTo.PartyId, Memory<byte>.Empty, cancellationToken);

            
           
            if(result.Success)
            {
                var reservation = new PartyReservation { PartyMembers = partyFrom.Players.Values, CustomData = customData };
                await _parties.CreateReservation(partyTo.PartyId, reservation, cancellationToken);


                lock (_syncRoot)
                {
                    if(_states.TryGetValue(partyFrom.PartyId, out var state))
                    {
                        state.Complete(result.Value);
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
