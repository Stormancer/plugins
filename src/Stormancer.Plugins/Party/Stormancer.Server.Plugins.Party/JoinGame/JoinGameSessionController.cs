using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party.JoinGame
{
    /// <summary>
    /// Provides API to join a gamesession associated with the party.
    /// </summary>
    [Service(Named = true, ServiceType = PartyPlugin.PARTY_SERVICEID)]
    public class JoinGamePartyController : ControllerBase
    {
        private readonly IGameSessions gameSessions;
        private readonly JoinGamesessionProxy gamesession;
        private readonly IPartyService party;

        /// <summary>
        /// Creates a new instance of <see cref="JoinGamePartyController"/>
        /// </summary>
        /// <param name="gameSessions"></param>
        /// <param name="gamesession"></param>
        /// <param name="party"></param>
        public JoinGamePartyController(IGameSessions gameSessions, JoinGamesessionProxy gamesession, IPartyService party)
        {
            this.gameSessions = gameSessions;
            this.gamesession = gamesession;
            this.party = party;
        }
        /// <summary>
        /// Gets a connection token to the game session the party is currently associated with.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string?> RequestReservationInCurrentGamesession(RequestContext<IScenePeerClient> ctx)
        {

            if (party.Settings.PublicServerData.TryGetValue("stormancer.partyStatus", out var partyStatus) && partyStatus == "gamesession" &&
                party.Settings.PublicServerData.TryGetValue("stormancer.partyStatus.details", out var gameSessionId))
            {

                return await gamesession.JoinInParty(gameSessionId, party.PartyId, ctx.RemotePeer.SessionId, ctx.CancellationToken);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Controller on gamesession to enable joining a gamesession in progress when joining a party connected to it.
    /// </summary>
    [Service(Named = true, ServiceType = "stormancer.plugins.gamesession")]
    public class JoinGamesessionController : ControllerBase
    {
        private readonly IGameSessionService gameSession;
        private readonly IUserSessions userSessions;
        private readonly IGameSessions gameSessions;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gameSession"></param>
        /// <param name="userSessions"></param>
        /// <param name="gameSessions"></param>
        public JoinGamesessionController(IGameSessionService gameSession, IUserSessions userSessions, IGameSessions gameSessions)
        {
            this.gameSession = gameSession;
            this.userSessions = userSessions;
            this.gameSessions = gameSessions;
        }

        /// <summary>
        /// Update party data in gamesession and returns a connection token.
        /// </summary>
        /// <param name="partyId"></param>
        /// <param name="sessionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [S2SApi]
        public async Task<string?> JoinInParty(string partyId, SessionId sessionId, CancellationToken cancellationToken)
        {
            var config = gameSession.GetGameSessionConfig();
            if(config == null)
            {
                return null;
            }
            var tuple = config.Teams.SelectMany(t => t.Parties.Select(party => new { party, team = t })).FirstOrDefault(p => p.party.PartyId == partyId);
            if (tuple == null)
            {
                return null;
            }

            var session = await userSessions.GetSessionById(sessionId, cancellationToken);
            if (session == null)
            {
                return null;
            }

            if (session.User == null)
            {
                return null;
            }

            if (!tuple.party.Players.ContainsKey(session.User.Id))
            {
                var team = new Team { TeamId = tuple.team.TeamId };
                var partyArg = new Models.Party { PartyId = partyId };
                partyArg.Players.Add(session.User.Id, new Player { SessionId = session.SessionId, UserId = session.User.Id });
                team.Parties.Add(partyArg);
                await gameSession.CreateReservationAsync(team, new Newtonsoft.Json.Linq.JObject { }, cancellationToken);

            }


            return await gameSessions.CreateConnectionToken(gameSession.GameSessionId, sessionId, TokenVersion.V3, cancellationToken);
        }
    }
}
