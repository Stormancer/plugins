using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Eos
{
    internal class EosFriendsEventHandler : IFriendsEventHandler
    {

        Task IFriendsEventHandler.OnAddingFriend(AddingFriendCtx ctx)
        {
            foreach (var friend in ctx.Friends)
            {
                if (friend.userInfos?.User != null && !friend.friend.UserIds.Any(p => p.Platform == Eos.PLATFORM_NAME) && friend.userInfos.User.TryGetEpicAccountId(out var accountId))
                {
                    friend.friend.UserIds.Add(new PlatformId(Eos.PLATFORM_NAME, accountId));
                }
            }

            return Task.CompletedTask;
        }
    }
}
