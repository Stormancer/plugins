using Stormancer.Server.Plugins.GameHistory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends.RecentlyMet
{
    /// <summary>
    /// Class using to store RecentlyMet related data in game session scenes.
    /// </summary>
    internal class RecentlyMetGameSessionState
    {
        public List<string> CurrentParticipants { get; } = new List<string>();

        public object SyncRoot  = new object();
    }
    internal class RecentlyMetGameHistoryEventHandler : IGameHistoryEventHandler
    {
        private readonly RecentlyMetFriendProxy _friends;
        private readonly RecentlyMetGameSessionState _state;

        public RecentlyMetGameHistoryEventHandler(RecentlyMetFriendProxy friends, RecentlyMetGameSessionState state)
        {
            _friends = friends;
            _state = state;
        }
        public async Task OnAddingParticipantToGame(OnAddingParticipantToGameContext ctx)
        {
            string[]? participants = null;
            lock(_state.SyncRoot)
            {
                _state.CurrentParticipants.Add(ctx.NewParticipant.Id.ToString("N"));
                if(_state.CurrentParticipants.Count> 1)
                {
                    participants = _state.CurrentParticipants.ToArray();
                }
            }
            if (participants != null)
            {
               await _friends.UpdateRecentlyPlayedWith(participants, CancellationToken.None);
            }
        }

        public Task OnAddingToHistory(OnAddingToHistoryContext ctx)
        {
            return Task.CompletedTask;
        }
    }
}
