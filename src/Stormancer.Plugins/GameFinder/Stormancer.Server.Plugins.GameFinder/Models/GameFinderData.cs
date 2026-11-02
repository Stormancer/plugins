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

using J2N.Collections.ObjectModel;
using Stormancer.Cluster;
using Stormancer.Core;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Stormancer.Server.Plugins.GameFinder
{
    internal class GameFinderData
    {
        internal GameFinderData(ISceneHost scene)
        {
            kind = scene.TemplateMetadata[GameFinderPlugin.METADATA_KEY];
        }

        private Dictionary<string, GameFinderRequestState> _waitingParties { get; } = new Dictionary<string, GameFinderRequestState>();
        private Dictionary<SessionId, Party> _peersToGroup { get; } = new Dictionary<SessionId, Party>();
        private Dictionary<string, SessionId> _userIdsToSessionIds { get; } = new Dictionary<string, SessionId>();
        private object _syncRoot = new object();

        public bool TryAddParty(Party party, [NotNullWhen(true)] out GameFinderRequestState? state)
        {
            lock (_syncRoot)
            {
                state = new GameFinderRequestState(party);
                state.State = RequestState.Ready;
                if(_waitingParties.TryAdd(party.PartyId, state))
                {
                    foreach(var (userId,player) in party.Players)
                    {
                        _peersToGroup[player.SessionId] = party;
                        _userIdsToSessionIds[userId] = player.SessionId;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public IReadOnlyDictionary<string, GameFinderRequestState> WaitingParties
        {
            get
            {
                lock (_syncRoot)
                {
                    return new ReadOnlyDictionary<string, GameFinderRequestState>(_waitingParties);
                }
            }
        }

        public bool TryGetStateForParty(string partyId, [NotNullWhen(true)] out GameFinderRequestState? state)
        {
            lock (_syncRoot)
            {
                return _waitingParties.TryGetValue(partyId, out state);
            }
        }

        public bool TryCompletePartySuccess(string partyId)
        {
            lock (_syncRoot)
            {
                if (TryRemove(partyId, out var state))
                {
                    
                    state.SetResult(new FindGameResult { Success = true });
                    return true;
                }
            }
            return false;
        }

        public bool TryCompletePartyError(string partyId, string errorMsg)
        {
            lock (_syncRoot)
            {
                if (TryRemove(partyId, out var state))
                {
                    state.SetResult(new FindGameResult { Success = false, ErrorMsg = errorMsg });
                    return true;
                }
            }
            return false;
        }

        public bool TryCancelParty(string partyId)
        {
            lock (_syncRoot)
            {
                if (TryRemove(partyId, out var state))
                {
                    state.SetCanceled();
                    return true;
                }
            }
            return false;
        }

        public bool TryGetParty(SessionId sessionId, [NotNullWhen(true)] out Party? party)
        {
            lock (_syncRoot)
            {
                return _peersToGroup.TryGetValue(sessionId, out party);
            }
        }

        private bool TryRemove(string partyId, [NotNullWhen(true)] out GameFinderRequestState? state)
        {
            if (_waitingParties.Remove(partyId, out state))
            {
                foreach(var member in state.Party.Players)
                {
                    _peersToGroup.Remove(member.Value.SessionId);
                    _userIdsToSessionIds.Remove(member.Value.UserId);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool IsRunning { get; set; }
        public string kind { get; }
        public TimeSpan interval { get; set; }
        public bool isReadyCheckEnabled { get; set; }
        public int readyCheckTimeout { get; set; }
        public bool acceptRequests { get; set; } = true;
    }
}
