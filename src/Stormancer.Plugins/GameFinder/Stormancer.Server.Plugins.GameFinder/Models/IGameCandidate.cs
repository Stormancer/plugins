using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stormancer.Server.Plugins.GameFinder.Models
{
    interface IGameCandidate
    {
        public string Id { get; }

        public IEnumerable<Team> Teams { get; }
    }

    static class IGameCandidateExtensions
    {
        public static IEnumerable<Group> AllGroups(this IGameCandidate game) => game.Teams.SelectMany(team => team.Groups);

        public static IEnumerable<Player> AllPlayers(this IGameCandidate game) => game.Teams.SelectMany(team => team.AllPlayers);
    }
}
