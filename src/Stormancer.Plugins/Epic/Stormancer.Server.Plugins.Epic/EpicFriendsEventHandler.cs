using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    internal class EpicFriendsEventHandler : IFriendsEventHandler
    {

        Task IFriendsEventHandler.OnAddingFriend(AddingFriendCtx ctx)
        {
            foreach (var friend in ctx.Friends)
            {
                if (friend.userInfos?.User != null && !friend.friend.UserIds.Any(p => p.Platform == EpicConstants.PLATFORM_NAME) && friend.userInfos.User.TryGetEpicAccountId(out var accountId))
                {
                    friend.friend.UserIds.Add(new PlatformId(EpicConstants.PLATFORM_NAME, accountId));
                }
            }

            return Task.CompletedTask;
        }
    }
}
