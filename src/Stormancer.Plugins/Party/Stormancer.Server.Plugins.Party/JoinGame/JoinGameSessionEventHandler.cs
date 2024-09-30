using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.Users;
using System.Threading;
using Stormancer.Server.Components;

namespace Stormancer.Server.Plugins.Party.JoinGame
{
    /// <summary>
    /// Contains the state related to the Join current gamesession functionality.
    /// </summary>
    internal class JoinGameSessionState
    {
        public Dictionary<string, string> UserIdToPartyId = new Dictionary<string, string>();
        internal object syncRoot = new object();
    }
    internal class JoinGameSessionEventHandler : IGameSessionEventHandler
    {
        private readonly PartyProxy party;
        private readonly IUserSessions _userSessions;
        private readonly JoinGameSessionState state;
        private readonly IConfiguration configuration;
        private readonly ILogger logger;

        public JoinGameSessionEventHandler(
            PartyProxy party,
            IUserSessions userSessions,
            JoinGameSessionState state,
            IConfiguration configuration,
            ILogger logger)
        {

            this.party = party;
            _userSessions = userSessions;
            this.state = state;
            this.configuration = configuration;
            this.logger = logger;
        }
        private bool IsEnabled
        {
            get
            {
                var configSection = configuration.GetValue<PartyConfigurationSection>("party");
                if (configSection != null && configSection.EnableGameSessionPartyStatus != null)
                {
                    return configSection.EnableGameSessionPartyStatus.Value;
                }
                else
                {
                    return true;
                }
            }
        }


        public async Task OnClientConnected(ClientConnectedContext ctx)
        {
            if (IsEnabled)
            {
                string? partyId = await _userSessions.GetSessionData<string>(ctx.Player.SessionId, "party", CancellationToken.None);

                bool shouldCreate = false;

                lock (state.syncRoot)
                {

                    if (partyId == null)
                    {
                        var config = ctx.GameSession.GetGameSessionConfig();
                        var party = config != null ? config.Teams.SelectMany(t => t.Parties).FirstOrDefault(p => p.Players.ContainsKey(ctx.Player.Player.UserId)) : null;

                        if (party != null)
                        {


                            partyId = party.PartyId;
                        }
                    }

                    if (partyId != null)
                    {
                        if (!state.UserIdToPartyId.Values.Contains(partyId))
                        {
                            shouldCreate = true;
                        }
                        state.UserIdToPartyId[ctx.Player.Player.UserId] = partyId;
                    }
                    else
                    {
                        logger.Log(LogLevel.Error, "party.gamesession.join", "Failed to find party for player.", new { ctx.GameSession.GameSessionId, ctx.Player.Player.UserId });
                    }
                }

                if (partyId != null)
                {
                    if (shouldCreate)
                    {
                        try
                        {
                            await party.AddPartyToGameSession(partyId, ctx.GameSession.GameSessionId, default);
                        }
                        catch (ClientException)
                        {
                            //party closed.
                        }
                        catch (Exception ex)
                        {
                            logger.Log(LogLevel.Error, "party", $"Failed to update the party status (id='{partyId}') with gamesession related informations :", ex);
                            throw;
                        }
                    }
                }
                else
                {
                    logger.Log(LogLevel.Warn, "party.gamesession.join", "No party found associated with player.", new { }, ctx.GameSession.GameSessionId, ctx.Player.Player.UserId);
                }

            }
        }



        public async Task OnClientLeaving(ClientLeavingContext ctx)
        {

            if (IsEnabled)
            {

                string? partyId = null;
                lock (state.syncRoot)
                {
                    //logger.Log(LogLevel.Info, "gamesession.joinedGameSession.leaving", $"Player {ctx.Player.Player.UserId} leaving {ctx.GameSession.GameSessionId}.", new { state.UserIdToPartyId });
                    state.UserIdToPartyId.Remove(ctx.Player.Player.UserId, out partyId);

                    if (partyId != null && state.UserIdToPartyId.Values.Contains(partyId))
                    {
                        partyId = null;
                    }
                }

                if (partyId != null)
                {
                    try
                    {

                        //logger.Log(LogLevel.Info, "gamesession.joinedGameSession.leaving", $"Removing  {ctx.GameSession.GameSessionId} from party {partyId}", new { state.UserIdToPartyId });
                        await party.RemovePartyFromGameSession(partyId, ctx.GameSession.GameSessionId, default);
                    }
                    catch (InvalidOperationException)
                    {
                        //Party already destroyed. Ignore the error.
                    }
                    catch (ClientException)
                    {
                        //Party closed.
                    }
                }


            }
        }

        public async Task OnGameSessionShutdown(Stormancer.Server.Plugins.GameSession.GameSessionShutdownContext ctx)
        {

            if (IsEnabled)
            {
                var partyIds = new List<string>();
                lock (state.syncRoot)
                {
                    //logger.Log(LogLevel.Info, "gamesession.joinedGameSession.shutdown", $"Gamesession {ctx.GameSession.GameSessionId} closing.", new { state.UserIdToPartyId });
                    foreach (var entry in state.UserIdToPartyId.Values.Distinct())
                    {
                        partyIds.Add(entry);
                    }
                }

                foreach (var partyId in partyIds)
                {
                    try
                    {
                        //logger.Log(LogLevel.Info, "gamesession.joinedGameSession.shutdown", $"Removing gamesession {ctx.GameSession.GameSessionId} from party {partyId}.", new { state.UserIdToPartyId });
                        await party.RemovePartyFromGameSession(partyId, ctx.GameSession.GameSessionId, default);
                    }
                    catch (Exception)
                    {
                        logger.Log(LogLevel.Warn, "gamesession.joinedGameSession.shutdown", $"An error occured while closing gamesession..", new { ctx.GameSession.GameSessionId });

                    }
                }
            }
        }



    }
}
