using MessagePack;
using Nest;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.GameFinder;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
	/// <summary>
	/// party settings for the server browser gamefinder.
	/// </summary>
	[MessagePackObject]
    public class ServerBrowserGamefinderArgs
    {
		/// <summary>
		/// Gets or sets the id of the game session to join.
		/// </summary>
		[Key("gamesessionId")]
		public string? GamesessionId { get; init; }
    }

	/// <summary>
	/// Server browser gamefinder algorithm.
	/// </summary>
    public class ServerBrowserGameFinderAlgorithm : IGameFinderAlgorithm
	{
		/// <summary>
		/// Creates a <see cref="ServerBrowserGameFinderAlgorithm"/> object.
		/// </summary>
		/// <param name="serializer"></param>
		/// <param name="gamesessions"></param>
		public ServerBrowserGameFinderAlgorithm(ISerializer serializer, IGameSessions gamesessions)
        {
            _serializer = serializer;
            _gamesessions = gamesessions;
        }
        private static JObject _default = new();
        private static JObject _emptyDataAnalytics = new JObject();
		private static Dictionary<string, int> _emptyMetrics = new Dictionary<string, int>();
        private readonly ISerializer _serializer;
        private readonly IGameSessions _gamesessions;

		///<inheritdoc/>
        public JObject ComputeDataAnalytics(GameFinderContext gameFinderContext)
		{
		
			return _emptyDataAnalytics;
		}
        ///<inheritdoc/>
        public async Task<GameFinderResult> FindGames(GameFinderContext gameFinderContext)
		{
			var result = new GameFinderResult();
			foreach(var party in gameFinderContext.WaitingParties)
			{
				var args = _serializer.Deserialize<ServerBrowserGamefinderArgs>(new MemoryStream(party.CustomData));

				if(string.IsNullOrEmpty(args.GamesessionId))
				{
                    gameFinderContext.SetFailed(party, "missingArgument?gamesessionId");
                }

				var reservation = await _gamesessions.CreateReservation(args.GamesessionId, new Models.Team(party), _default, default);

				if (reservation is null)
				{
					gameFinderContext.SetFailed(party, "gameFull");
				}
				else
				{
					AddPartyToExistingGame(result, args.GamesessionId, party);
				}
            }

			return result;
		}
        private void AddPartyToExistingGame(GameFinderResult results, string id, Stormancer.Server.Plugins.Models.Party party)
        {
            var game = results.Games.FirstOrDefault(g => g.Id == id);
            if (game is null)
            {
                game = new ExistingGame(id);
                results.Games.Add(game);
            }

            var gameTeam = game.Teams.FirstOrDefault();
            if (gameTeam != null)
            {
                gameTeam.Parties.Add(party);
            }
            else
            {
                game.Teams.Add(new Team(party) { TeamId = "0" });
            }

        }
        ///<inheritdoc/>
        public Dictionary<string, int> GetMetrics()
		{
			return _emptyMetrics;
		}
        ///<inheritdoc/>
        public void RefreshConfig(string id, dynamic config)
		{
			
		}
	}

	/// <summary>
	/// The gamefinder session resolver for the server browser game finder.
	/// </summary>
	public class ServerBrowserGameFinderResolver : IGameFinderResolver
	{
        ///<inheritdoc/>
        public Task PrepareGameResolution(GameFinderResult gameFinderResult)
		{
			return Task.CompletedTask;
		}

        ///<inheritdoc/>
        public void RefreshConfig(string id, dynamic config)
		{
			
		}

        ///<inheritdoc/>
        public Task ResolveGame(IGameResolverContext gameCtx)
		{
			//Does not support creating games.
			return Task.CompletedTask;
		}
	}
}
