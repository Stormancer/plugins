using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
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
        internal Dictionary<string, string> MergingCommands { get; } = new Dictionary<string, string>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="into"></param>
        public void TryMerge(Models.Party from, Models.Party into)
        {
            MergingCommands.Add(from.PartyId, into.PartyId);
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
