using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Models
{
    /// <summary>
    /// Represents a party in a game session.
    /// </summary>
    [MessagePackObject]
    public class PartySummary
    {
        /// <summary>
        /// Gets the id of the party.
        /// </summary>
        [Key(0)]
        public required string Id { get; init; }

        /// <summary>
        /// Gets the id of the party leader.
        /// </summary>
        [Key(1)]
        public required string LeaderId { get; init; }

        /// <summary>
        /// Gets the list of players in the party.
        /// </summary>
        [Key(2)]
        public HashSet<string> Members { get; } = new();
        
        /// <summary>
        /// Creates a new <see cref="PartySummary"/> from a <see cref="Party"/>.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static PartySummary Create(Party party)
        {
            var summary = new PartySummary { Id = party.PartyId, LeaderId = party.PartyLeaderId };
            foreach (var member in party.Players)
            {
                summary.Members.Add(member.Key);
            }
            return summary;
        }

        /// <summary>
        /// Creates a copy of this <see cref="PartySummary"/>.
        /// </summary>
        /// <returns></returns>
        public PartySummary Clone()
        {
            var party = new PartySummary { Id = Id, LeaderId = LeaderId };
            foreach (var player in Members)
            {
                party.Members.Add(player);
            }
            return party;
        }
    }
}
