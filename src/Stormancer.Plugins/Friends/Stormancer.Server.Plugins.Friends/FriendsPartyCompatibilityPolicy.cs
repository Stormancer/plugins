using Stormancer.Abstractions.Server.GameFinder;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    internal class FriendsPartyCompatibilityPolicy : IPartyCompatibilityPolicy
    {
        private readonly IFriendsService _friendsService;

        public FriendsPartyCompatibilityPolicy(IFriendsService friendsService)
        {
            _friendsService = friendsService;
        }
        public async Task<CompatibilityTestResult> AreCompatible(Party party1, Party party2, object context)
        {
            var blockLists = await _friendsService.GetBlockedLists(party1.Players.Select(p => p.Value.UserId).Concat(party2.Players.Select(p => p.Value.UserId)), CancellationToken.None);



            var party1BlockList = party1.Players
                .Select(p => p.Value.UserId)
                .SelectMany(userId => blockLists.TryGetValue(userId, out var list) ? list : Enumerable.Empty<string>()).Distinct();

            var party2BlockList = party1.Players
                .Select(p => p.Value.UserId)
                .SelectMany(userId => blockLists.TryGetValue(userId, out var list) ? list : Enumerable.Empty<string>()).Distinct();


            if(party1.Players.Select(p=>p.Value.UserId).Any(userId=>party2BlockList.Contains(userId))
                || party2.Players.Select(p => p.Value.UserId).Any(userId => party1BlockList.Contains(userId)))
            {
                return new CompatibilityTestResult(false, "blocklist");
            }
            else
            {
                return new CompatibilityTestResult(true);
            }


        }
    }
}
