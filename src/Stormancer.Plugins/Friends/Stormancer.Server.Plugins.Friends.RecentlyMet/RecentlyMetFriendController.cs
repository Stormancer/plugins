using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends.RecentlyMet
{
    [Service(ServiceType = FriendsConstants.SERVICE_ID)]
    internal class RecentlyMetFriendController : ControllerBase
    {
        private readonly IFriendsService _friends;

        public RecentlyMetFriendController(IFriendsService friends)
        {



            _friends = friends;
        }

        [S2SApi]
        public async Task UpdateRecentlyPlayedWith(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            foreach (var userId in userIds)
            {
                var friends = await _friends.GetFriends(userId, cancellationToken);
                foreach (var friendId in userIds.Where(id => id != userId))
                {
                    if (!friends.Any(f => f.TryGetIdForPlatform(Users.Constants.PROVIDER_TYPE_STORMANCER, out var id) && id == friendId))
                    {
                        var dto = new FriendListUpdateDto
                        {
                            Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                            Data = new Friend
                            {
                                Status = new Dictionary<string, FriendConnectionStatus> { [Users.Constants.PROVIDER_TYPE_STORMANCER] = FriendConnectionStatus.Disconnected },
                                Tags = new List<string> { "recentlyMet" },
                                UserIds = new List<Users.PlatformId> {
                            new Users.PlatformId(Users.Constants.PROVIDER_TYPE_STORMANCER, friendId) }
                            }
                        };
                        await _friends.ProcessUpdates(userId, Enumerable.Repeat(dto, 1));
                    }
                }
            }
        }
    }
}
