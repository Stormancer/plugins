// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Friends.Data;
using Stormancer.Server.Plugins.Friends.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    //TODO: update to support distributed scene
    class FriendsRepository
    {
        private class UserContainer
        {
            public UserContainer(SessionId sessionId, Guid key)
            {
                SessionId = sessionId;
                Key = key;
            }
            public SessionId SessionId { get; }
            public Guid Key { get; }
            public UserFriendListConfig? Config { get; set; }
            public List<Friend> Friends { get; } = new List<Friend>();
            public User User { get; internal set; }
        }

        //[pId=>{SessionId}]
        private readonly ConcurrentDictionary<Guid, UserContainer> _platformIds = new();

        //[SessionId=>UserContainer]
        private readonly ConcurrentDictionary<SessionId, UserContainer> _peers = new();
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;

        public FriendsRepository(ISceneHost scene, ISerializer serializer)
        {
            _scene = scene;
            _serializer = serializer;
        }
        public Task AddPeer(Guid key, IScenePeerClient peer, User user, UserFriendListConfig statusConfig)
        {
            if (statusConfig == null)
            {
                throw new ArgumentNullException("statusConfig");
            }
            var v = new UserContainer(peer.SessionId, key) { Config = statusConfig, User = user };
            _platformIds.AddOrUpdate(key, v, (k, old) => v);
            _peers.AddOrUpdate(peer.SessionId, v, (k, old) => v);
            return Task.FromResult(true);
        }

        public Task<UserFriendListConfig?> GetStatusConfig(PlatformId key)
        {

            if (_platformIds.TryGetValue(Guid.Parse(key.PlatformUserId), out var p))
            {
                return Task.FromResult(p.Config);
            }
            else
            {
                return Task.FromResult(default(UserFriendListConfig));
            }

        }

        public Task<bool> UpdateStatusConfig(Guid key, UserFriendListConfig newConfig)
        {
            UserContainer p;
            if (_platformIds.TryGetValue(key, out p))
            {
                p.Config = newConfig;
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }

        }

        public Task<(UserFriendListConfig? config, Guid userId)> RemovePeer(SessionId sessionId)
        {

            if (_peers.TryRemove(sessionId, out var container))
            {
                if (_platformIds.TryRemove(container.Key, out _))
                {

                    return Task.FromResult(((UserFriendListConfig?)container.Config, container.Key));
                }
            }
            return Task.FromResult(((UserFriendListConfig?)null, default(Guid)));
        }

        public IEnumerable<SessionId> GetSessionIds(IEnumerable<string> userIds)
        {
            foreach (var userId in userIds)
            {
                if (_platformIds.TryGetValue(Guid.Parse(userId), out var container))
                {
                    yield return container.SessionId;
                }
            }

        }
        public void ApplyFriendListUpdates(Guid ownerId, IEnumerable<FriendListUpdateDto> dtos)
        {
            if (_platformIds.TryGetValue(ownerId, out var container))
            {
                lock (container)
                {
                    foreach (var dto in dtos)
                    {
                        ProcessFriendListUpdateLocally(container, dto);
                    }
                }
                Notify(dtos, ownerId.ToString("N"));
            }
        }
        public void ApplyFriendListUpdate(Guid ownerId, FriendListUpdateDto dto)
        {
            if (_platformIds.TryGetValue(ownerId, out var container))
            {
                lock (container)
                {
                    ProcessFriendListUpdateLocally(container, dto);
                }
                Notify(dto, ownerId.ToString("N"));
            }
        }

        private void ProcessFriendListUpdateLocally(UserContainer container, FriendListUpdateDto dto)
        {
            var f = container.Friends.FirstOrDefault(f => f.UserIds.Intersect(dto.Data.UserIds).Any());

            switch (dto.Operation)
            {
                case FriendListUpdateDtoOperation.AddOrUpdate:
                    if (f != null)
                    {
                        foreach (var userId in dto.Data.UserIds)
                        {
                            if (!f.UserIds.Contains(userId))
                            {
                                f.UserIds.Add(userId);
                            }


                        }
                        f.CustomData = dto.Data.CustomData;
                        f.Tags = dto.Data.Tags;
                        foreach (var (key, status) in dto.Data.Status)
                        {
                            f.Status[key] = status;
                        }
                        dto.Data.Status = f.Status;
                    }
                    else
                    {
                        container.Friends.Add(dto.Data);
                    }
                    break;

                case FriendListUpdateDtoOperation.Remove:
                    if (f != null)
                    {
                        foreach (var userId in dto.Data.UserIds)
                        {
                            f.UserIds.Remove(userId);
                            f.Status.Remove(userId.Platform);
                        }
                        if (!f.UserIds.Any())
                        {
                            container.Friends.Remove(f);
                        }
                    }
                    break;
                case FriendListUpdateDtoOperation.UpdateStatus:
                    if (f != null)
                    {
                        foreach (var (key, status) in dto.Data.Status)
                        {
                            f.Status[key] = status;
                        }
                        dto.Data.Status = f.Status;
                    }
                    break;
            }
        }







        private Friend CreateFriendDtoDetailed(string friendId, UserFriendListConfig? friendConfig, MemberRecordStatus recordStatus, List<string> tags, JsonDocument customData)
        {
            Friend friend;
            if (recordStatus == MemberRecordStatus.Accepted)
            {
                FriendConnectionStatus status;

                if (friendConfig == null) //Friend not connected.
                {
                    status = FriendConnectionStatus.Disconnected;
                }
                else //Friend connected
                {
                    status = ComputeStatus(friendConfig, true);
                }

                friend = new Friend
                {
                    UserIds = new() { new PlatformId { Platform = Stormancer.Server.Plugins.Users.Constants.PROVIDER_TYPE_STORMANCER, PlatformUserId = friendId.ToString() } },
                    Status = new Dictionary<string, FriendConnectionStatus> { [Users.Constants.PROVIDER_TYPE_STORMANCER] = status },
                    Tags = tags,
                    CustomData = JsonSerializer.Serialize(customData)
                };



            }
            else
            {
                friend = new Friend
                {
                    UserIds = new() { new PlatformId { Platform = Stormancer.Server.Plugins.Users.Constants.PROVIDER_TYPE_STORMANCER, PlatformUserId = friendId.ToString() } },

                    Status = new Dictionary<string, FriendConnectionStatus> { [Users.Constants.PROVIDER_TYPE_STORMANCER] = FriendConnectionStatus.Disconnected },


                };
            }

            switch (recordStatus)
            {

                case MemberRecordStatus.SentInvitation:
                    friend.Tags.Add("friends.invitation.sent");
                    break;
                case MemberRecordStatus.PendingInvitation:
                    friend.Tags.Add("friends.invitation.received");
                    break;
                case MemberRecordStatus.DeletedByFriend:
                    break;
                case MemberRecordStatus.Blocked:
                    friend.Tags.Add("friends.blocked");
                    break;
            }

            return friend;
        }

        private async Task<Friend> CreateFriendDtoDetailed(MemberRecord record)
        {
            var config = await GetStatusConfig(new PlatformId(Users.Constants.PROVIDER_TYPE_STORMANCER, record.FriendId.ToString("N")));

            return CreateFriendDtoDetailed(record.FriendId.ToString("N"), config, record.Status, record.Tags, record.CustomData);

        }


        private FriendConnectionStatus ComputeStatus(UserFriendListConfig config, bool online)
        {
            if (!online)
            {
                return FriendConnectionStatus.Disconnected;
            }
            else
            {
                switch (config.Status)
                {
                    case FriendListStatusConfig.Invisible:
                        return FriendConnectionStatus.Disconnected;
                    case FriendListStatusConfig.Away:
                        return FriendConnectionStatus.Away;
                    case FriendListStatusConfig.Online:
                        return FriendConnectionStatus.Connected;
                    default:
                        return FriendConnectionStatus.Disconnected;
                }
            }
        }

        private void Notify(FriendListUpdateDto data, IEnumerable<string> userIds) => Notify(Enumerable.Repeat(data, 1), userIds);
        private void Notify(FriendListUpdateDto data, string userId) => Notify(Enumerable.Repeat(data, 1), Enumerable.Repeat(userId, 1));
        private void Notify(IEnumerable<FriendListUpdateDto> data, string userId) => Notify(data, Enumerable.Repeat(userId, 1));

        private void Notify(IEnumerable<FriendListUpdateDto> data, IEnumerable<string> userIds)
        {

            var sessionIds = GetSessionIds(userIds);

            BroadcastToPlayers(sessionIds, "friends.notification", data);

        }

        private void BroadcastToPlayers<T>(IEnumerable<SessionId> peers, string route, T data)
        {
            BroadcastToPlayers(peers, route, (stream, serializer) => { serializer.Serialize(data, stream); });
        }

        private void BroadcastToPlayers(IEnumerable<SessionId> peers, string route, Action<IBufferWriter<byte>, ISerializer> writer)
        {
            _scene.Send(new MatchArrayFilter(peers), route, static (s, userData) =>
            {
                var (writer, serializer) = userData;
                writer(s, serializer);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED, (writer, _serializer));

        }

        internal bool TryGetBlockList(Guid userId, [NotNullWhen(true)] out IEnumerable<string>? blockList)
        {
            if (_platformIds.TryGetValue(userId, out var container))
            {
                lock (container)
                {
                    var list = new List<string>();

                    foreach (var item in container.Friends)
                    {
                        if (item.Tags.Contains("friends.blocked") && item.TryGetIdForPlatform(Users.Constants.PROVIDER_TYPE_STORMANCER, out var uid))
                        {
                            list.Add(uid);
                        }
                    }
                    blockList = list;
                    return true;
                }
            }
            else
            {
                blockList = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the list of friend lists currently managed by the system that contain the userId, and for each of them returns the corresponding friend object.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        internal async IAsyncEnumerable<(Guid ownerId, User owner, Friend friend)> GetListsContainingMemberAsync(PlatformId userId)
        {
            foreach (var (sessionId, container) in _peers)
            {
                lock (container)
                {
                    var friend = container.Friends.FirstOrDefault(f => f.UserIds.Contains(userId));
                    if (friend != null)
                    {
                        yield return (container.Key, container.User, friend);
                    }
                }
            }
        }
    }


}
