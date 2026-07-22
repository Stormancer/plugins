using MessagePack;
using Stormancer.Server;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Gamesessions.Browser
{
    internal class GameSessionReservations
    {
        private readonly IHost _host;
        private readonly GameSessionsRepository _gamesessions;
        private readonly IClusterSerializer _serializer;

        public GameSessionReservations(IHost host, GameSessionsRepository gamesessions, IClusterSerializer serializer)
        {
            _host = host;
            _gamesessions = gamesessions;
            _serializer = serializer;
        }

        public void Initialize()
        {
            _host.RegisterAppFunction("gamesessions.getReservations", OnGetReservations);
        }

        private async Task OnGetReservations(IAppFunctionContext ctx)
        {
            var rq = await _serializer.DeserializeAsync<GetGamesessionReservationsRequest>(ctx.Input, CancellationToken.None);

            foreach (var gameSession in _gamesessions.LocalGameSessions)
            {
                var state = gameSession.Scene.DependencyResolver.Resolve<ReservationsState>();
                if (state.MemberUserIds.Contains(rq.UserId))
                {

                    ctx.Output.WriteObject(new GetGamesessionReservationsResult { GamesessionId = gameSession.Scene.Id }, _serializer);
                    return;
                }
            }
            ctx.Output.WriteObject(new GetGamesessionReservationsResult { GamesessionId = null }, _serializer);
            return;
        }

        public async Task<GetGamesessionReservationsResult> GetReservationsAsync(string userId, CancellationToken cancellationToken)
        {
            using var request = await _host.CreateAppFunctionRequest("gamesessions.getReservations", cancellationToken);

            request.Input.WriteObject(new GetGamesessionReservationsRequest { UserId = userId }, _serializer);
            request.Send();
            List<GetGamesessionReservationsResult> results = new();
            await foreach (var response in request.Results)
            {
                if (response.IsSuccess)
                {
                    results.Add(await _serializer.DeserializeAsync<GetGamesessionReservationsResult>(response.Output, cancellationToken));
                }
            }

            foreach (var result in results)
            {
                return result;
            }

            return new GetGamesessionReservationsResult { GamesessionId = null };
        }
    }

    /// <summary>
    /// Request object to obtain the reservations of a given player.
    /// </summary>
    [MessagePackObject]
    public class GetGamesessionReservationsRequest
    {
        /// <summary>
        /// Gets or sets the id of the user whose reservations we want to request.
        /// </summary>
        public required string UserId { get; init; }
    }

    /// <summary>
    /// Results of a get reservations 
    /// </summary>
    [MessagePackObject]
    public class GetGamesessionReservationsResult
    {
        /// <summary>
        /// Gets the id of a gamesession containing a reservation.
        /// </summary>
        public string? GamesessionId { get; init; }
    }

    internal class ReservationsState
    {
        public HashSet<string> MemberUserIds { get; } = new HashSet<string>();
    }
}
