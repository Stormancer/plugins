using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder.Models
{
    /// <summary>
    /// This class represents an open game session that can accept players from the GameFinder.
    /// </summary>
    public class OpenGameSession
    {
        /// <summary>
        /// The scene Id of the game session.
        /// </summary>
        public string SceneId { get => requestContext.RemotePeer.SceneId; }

        /// <summary>
        /// Game-specific data for the game session.
        /// </summary>
        public JObject Data { get; set; }

        /// <summary>
        /// Whether this game session still accepts players.
        /// </summary>
        public bool IsOpen { get; private set; } = true;

        /// <summary>
        /// Close this game session, marking it to be removed from the game finder.
        /// </summary>
        public void Close()
        {
            IsOpen = false;
        }

        /// <summary>
        /// How many game finder passes this game session has been open for.
        /// </summary>
        public int NumGameFinderPasses { get; internal set; } = 0;

        /// <summary>
        /// The time at which this game session was opened.
        /// </summary>
        public DateTime CreationTimeUtc { get; internal set; } = DateTime.UtcNow;

        internal TaskCompletionSource<object?> Tcs { get; } = new TaskCompletionSource<object?>();
        
        private RequestContext<IScenePeer> requestContext;

        /// <summary>
        /// Notify the game session about new incoming teams.
        /// </summary>
        /// <remarks>
        /// Game sessions that are not public have a list of authorized teams.
        /// Thus it is required to add new members to this list before they connect to the game session.
        /// </remarks>
        /// <param name="teams">Teams that have been added to the game session by the GameFinder.</param>
        /// <returns></returns>
        internal async Task RegisterTeams(IEnumerable<Team> teams)
        {
            var gsTeams = teams.Select(t => new GameSession.Team
            {
                Groups = t.Groups.Select(g => new GameSession.Group
                {
                    PlayersId = g.Players.Select(p => p.UserId).ToList()
                }).ToList()
            });

            await requestContext.SendValue(stream => requestContext.RemotePeer.Serializer().Serialize(gsTeams, stream));
        }

        internal void Complete()
        {
            Tcs.SetResult(null);
        }

        internal OpenGameSession(JObject data, RequestContext<IScenePeer> requestContext)
        {
            Data = data;
            this.requestContext = requestContext;
        }
    }
}
