using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    public class SimplePartyMergingAlgorithm : IPartyMergingAlgorithm
    {
        private readonly int _partySize;

        public SimplePartyMergingAlgorithm(int partySize)
        {
            _partySize = partySize;
        }

        /// <summary>
        /// Merges the parties waiting in the merger.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task Merge(PartyMergingContext ctx)
        {
            foreach(var party in ctx.WaitingParties.OrderByDescending(p=>p.Players.Count))
            {
                 
                if(party.Players.Count < _partySize & !ctx.IsMerged(party))
                {
                    var mergeList = new List<Models.Party>();
                    int getCurrentPartySize(Models.Party into, List<Models.Party> currentFrom)
                    {
                        return into.Players.Count + currentFrom.Sum(p=>p.Players.Count);
                    }
                        
                    foreach(var fromParty in ctx.WaitingParties.Where(p=>!ctx.IsMerged(p)).OrderBy(p=>p.Players.Count))
                    {
                        if(getCurrentPartySize(party,mergeList) + fromParty.Players.Count < _partySize)
                        {
                            mergeList.Add(fromParty);
                        }
                        else
                        {
                            //Stop searching, we won't find any party that could fit in the currently examined party because the sequence is ordered by increasing player count.
                            break;
                        }

                    }

                    if(getCurrentPartySize(party, mergeList) == _partySize)
                    {
                        foreach(var from in mergeList)
                        {
                            ctx.Merge(from, party, new Newtonsoft.Json.Linq.JObject());
                        }
                    }
                }

            }

            return Task.CompletedTask;
        }
    }
}
