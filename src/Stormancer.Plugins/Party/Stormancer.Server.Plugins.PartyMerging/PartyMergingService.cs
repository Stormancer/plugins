using Stormancer.Core;
using System;
using System.Collections.Generic;
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
                _partyId = partyId;
                LinkCancellationToken(cancellationToken);
            }
            private readonly TaskCompletionSource _completedTcs = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly string _partyId;
            private CancellationTokenSource _cts;
            public Task WhenCompletedAsync()
            {
                return _completedTcs.Task;
            }
            public void Dispose()
            {
                _completedTcs.TrySetException(new OperationCanceledException());
                _cts.Dispose();
            }
            
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
                _cts.Regi
            }
        }
        private readonly ISceneHost _scene;

        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, PartyMergingState> _states = new Dictionary<string, PartyMergingState>();

        public PartyMergingService(ISceneHost scene)
        {
            _scene = scene;
        }

        public async Task StartMergeParty(string partyId, CancellationToken cancellationToken)
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

                    await state.WhenCompletedAsync();
                }
                
            }
            finally
            {
                lock(_syncRoot)
                {
                    _states.Remove(partyId);
                }
            }
        }
    }
}
