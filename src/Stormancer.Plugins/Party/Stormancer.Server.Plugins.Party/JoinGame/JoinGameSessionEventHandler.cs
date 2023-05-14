﻿using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.Users;
using System.Threading;

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
                string? partyId = await _userSessions.GetSessionData<string>(ctx.Player.Peer.SessionId, "party", CancellationToken.None);



                lock (state.syncRoot)
                {
                    if (partyId != null)
                    {
                        state.UserIdToPartyId[ctx.Player.Player.UserId] = partyId;
                    }
                    else
                    {
                        var party = ctx.GameSession.GetGameSessionConfig().Teams.SelectMany(t => t.Parties).FirstOrDefault(p => p.Players.ContainsKey(ctx.Player.Player.UserId));

                        if (party != null)
                        {
                            if (!state.UserIdToPartyId.Values.Contains(party.PartyId))
                            {
                                partyId = party.PartyId;
                            }
                            state.UserIdToPartyId[ctx.Player.Player.UserId] = party.PartyId;

                            partyId = party.PartyId;
                        }
                    }
                }

                if (partyId != null)
                {
                    try
                    {
                        await party.UpdatePartyStatusAsync(partyId, null, "gamesession", ctx.GameSession.GameSessionId, default);
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Error, "party", $"Failed to update the party status (id='{partyId}') with gamesession related informations :", ex);
                        throw;
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
                    state.UserIdToPartyId.Remove(ctx.Player.Player.UserId, out partyId);

                    if (partyId != null && state.UserIdToPartyId.Values.Contains(partyId))
                    {
                        partyId = null;
                    }
                }

                if (partyId != null)
                {
                    await party.UpdatePartyStatusAsync(partyId, "gamesession", "", null, default);
                }


            }
        }


    }
}
