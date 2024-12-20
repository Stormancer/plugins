﻿using Stormancer.Abstractions.Server.GameFinder;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    internal class FriendsPartyCompatibilityPolicy : IPartyCompatibilityPolicy, IPartyEventHandler
    {
        private readonly IFriendsService _friendsService;

        public FriendsPartyCompatibilityPolicy(IFriendsService friendsService)
        {
            _friendsService = friendsService;
        }

        public async Task<CompatibilityTestResult> AreCompatible(Plugins.Models.Party party1, Stormancer.Server.Plugins.Models.Party party2, object context)
        {
            var blockLists = await _friendsService.GetBlockedLists(party1.Players.Select(p => p.Value.UserId).Concat(party2.Players.Select(p => p.Value.UserId)), CancellationToken.None);

            var party1BlockList = party1.Players
                .Select(p => p.Value.UserId)
                .SelectMany(userId => blockLists.TryGetValue(userId, out var list) ? list : Enumerable.Empty<string>()).Select(userId => Guid.Parse(userId)).Distinct();

            var party2BlockList = party2.Players
                .Select(p => p.Value.UserId)
                .SelectMany(userId => blockLists.TryGetValue(userId, out var list) ? list : Enumerable.Empty<string>()).Select(userId => Guid.Parse(userId)).Distinct();


            if (party1.Players.Select(p => Guid.Parse(p.Value.UserId)).Any(userId => party2BlockList.Contains(userId))
                || party2.Players.Select(p => Guid.Parse(p.Value.UserId)).Any(userId => party1BlockList.Contains(userId)))
            {
                return new CompatibilityTestResult(false, "blocklist");
            }
            else
            {
                return new CompatibilityTestResult(true);
            }


        }

        async Task IPartyEventHandler.OnJoining(Stormancer.Server.Plugins.Party.JoiningPartyContext ctx)
        {
            if (ctx.Session.User != null)
            {
                var ids = ctx.Party.PartyMembers.Select(m => m.Value.UserId).Concat(new[] { ctx.Session.User.Id }).ToArray();
                var blockLists = await _friendsService.GetBlockedLists(ids, CancellationToken.None);

                foreach (var (id, blockList) in blockLists)
                {
                    if (blockList.Contains(ctx.Session.User.Id))
                    {
                        ctx.Accept = false;
                        ctx.Reason = "blocklist";
                    }
                }

                if(blockLists.TryGetValue(ctx.Session.User.Id,out var newPlayerBlockList))
                {
                    foreach(var id in ctx.Party.PartyMembers.Select(m => m.Value.UserId))
                    {
                        if(newPlayerBlockList.Contains(id))
                        {
                            ctx.Accept = false;
                            ctx.Reason = "blocklist";
                        }
                    }
                }
            }

        }

    }
}
