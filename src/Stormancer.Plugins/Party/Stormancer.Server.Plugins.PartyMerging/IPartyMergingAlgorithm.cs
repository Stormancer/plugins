using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    /// <summary>
    /// A Party merging command.
    /// </summary>
    public class MergingCommand
    {
        internal MergingCommand(Models.Party from, Models.Party into, JObject customData)
        {
            From = from;
            Into = into;
            CustomData = customData;
        }
        public Models.Party From { get;  }
        public Models.Party Into { get; }
        public JObject CustomData { get; }
    }

    /// <summary>
    /// Context passed to a Party merging algorithm.
    /// </summary>
    public class PartyMergingContext
    {
        internal PartyMergingContext(IEnumerable<Models.Party> parties)
        {
            WaitingParties = parties;
        }

        /// <summary>
        /// Parties currently waiting to be merged.
        /// </summary>
        public IEnumerable<Models.Party> WaitingParties { get; }

        /// <summary>
        /// Party merging commands that should be attempted
        /// </summary>
        public Dictionary<string, MergingCommand> MergeCommands { get; } = new Dictionary<string, MergingCommand>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Party the players are moved from as part of the merging operation.</param>
        /// <param name="into">Party the players are moved to as part of the merging operation.</param>
        /// <param name="customData">Custom data passed to the into party when a reservation for the players from the 'from' party is created.</param>
        public void Merge(Models.Party from, Models.Party into, JObject customData)
        {
            if(from.PartyId == into.PartyId)
            {
                throw new InvalidOperationException($"Cannot merge {from.PartyId} into itself.");
            }
            MergeCommands.Add(from.PartyId, new MergingCommand(from,into, customData));
        }

        /// <summary>
        /// Returns a boolean indicating whether the party in argument as already been merged during the current pass.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public bool IsMerged(Models.Party from)
        {
            return MergeCommands.ContainsKey(from.PartyId);
        }
    }

    /// <summary>
    /// Provides an implementation of a party merging algorithm.
    /// </summary>
    public interface IPartyMergingAlgorithm
    {
        /// <summary>
        /// Called regularly by the party merger and implements the merging logic.
        /// </summary>
        /// <returns></returns>
        Task Merge(PartyMergingContext ctx);
    }
}
