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

using Lucene.Net.Documents;
using Microsoft.AspNetCore.Builder;
using Nest;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Friends.Data;
using Stormancer.Server.Plugins.Friends.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    internal class FriendsService : IFriendsService
    {


        private const string LIST_TYPE = "friends";

        private readonly FriendsRepository _channel;
        private readonly MembersStorageService _storage;
        private readonly ISceneHost _scene;
        private readonly IUserService _users;
        private readonly IUserSessions _sessions;
        private readonly ISerializer _serializer;
        private readonly ILogger _logger;

        public FriendsService(
            FriendsRepository repository,
            MembersStorageService storage,
            IEnvironment environment,
            ISceneHost scene,
            ILogger logger,
            IUserService users,
            IUserSessions sessions,

            ISerializer serializer
            )
        {
            _logger = logger;
            _scene = scene;
            _channel = repository;
            _storage = storage;
            _users = users;
            _sessions = sessions;
            _serializer = serializer;
        }

        public async Task Invite(User user, User friend, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("userId");
            }

            if (friend == null)
            {
                throw new ArgumentNullException("friendId");
            }

            if (user.Id == friend.Id)
            {
                throw new InvalidOperationException("Cannot invite oneself as a friend.");
            }

            if (await IsInFriendList(user.Id, friend.Id))
            {
                return;
            }
            var ownerId = Guid.Parse(user.Id);
            var invitedUserId = Guid.Parse(friend.Id);
            var ownerRecord = await _storage.GetListMemberAsync(new MemberId(invitedUserId, ownerId));
            var invitedUserRecord = await _storage.GetListMemberAsync(new MemberId(ownerId, invitedUserId));

            var builder = new MembersOperationsBuilder();
            if (ownerRecord == null)
            {
                ownerRecord = new MemberRecord(new MemberId(invitedUserId, ownerId), MemberRecordStatus.SentInvitation, LIST_TYPE);
                builder = builder.Add(ownerRecord);
            }
            if (invitedUserRecord == null)
            {
                invitedUserRecord = new MemberRecord(new MemberId(ownerId, invitedUserId), MemberRecordStatus.PendingInvitation, LIST_TYPE);
                builder = builder.Add(invitedUserRecord);
            }

            builder = (ownerRecord, invitedUserRecord) switch
            {
                ({ Status: MemberRecordStatus.Accepted }, { Status: MemberRecordStatus.Accepted }) => builder,
                ({ }, { Status: MemberRecordStatus.Accepted }) => builder
                    .Update(new MemberId(invitedUserId, ownerId), m => m.Status = MemberRecordStatus.Accepted),
                ({ }, { Status: MemberRecordStatus.SentInvitation }) => builder
                    .Update(new MemberId(invitedUserId, ownerId), m => m.Status = MemberRecordStatus.Accepted)
                    .Update(new MemberId(ownerId, invitedUserId), m => m.Status = MemberRecordStatus.Accepted),
                ({ }, { Status: MemberRecordStatus.DeletedByFriend }) => builder
                    .Update(new MemberId(invitedUserId, ownerId), m => m.Status = MemberRecordStatus.Accepted)
                    .Update(new MemberId(ownerId, invitedUserId), m => m.Status = MemberRecordStatus.Accepted),
                _ => builder
            };

            await _storage.SaveBatchAsync(builder);
            await NotifyChangesAsync(builder);
        }

        private async Task NotifyChangesAsync(MembersOperationsBuilder builder)
        {
            foreach (var operation in builder.Operations)
            {
                Guid destinationUserId = operation.Id.ListOwnerId;
                FriendListUpdateDto? dto;
                switch (operation.Type)
                {


                    case MembersOperationType.Add:
                        Debug.Assert(operation.Record != null);
                        var record = operation.Record;
                        dto = new FriendListUpdateDto
                        {
                            Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                            ItemId = operation.Id.UserId.ToString(),
                            Data = await CreateFriendDtoDetailed(record)
                        };
                        break;
                    case MembersOperationType.Update:
                        record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                        Debug.Assert(record != null);
                        operation.Updater(record);
                        dto = new FriendListUpdateDto
                        {
                            Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                            ItemId = operation.Id.UserId.ToString(),
                            Data = await CreateFriendDtoDetailed(record)
                        };
                        break;
                    case MembersOperationType.Delete:
                        record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                        Debug.Assert(record != null);
                        dto = new FriendListUpdateDto
                        {
                            Operation = FriendListUpdateDtoOperation.Remove,
                            ItemId = operation.Id.UserId.ToString(),
                            Data = new Friend { Status = FriendConnectionStatus.Disconnected, UserId = operation.Id.UserId.ToString() }
                        };

                        break;
                    default:
                        dto = null;
                        break;
                }
                if (dto != null)
                {
                    await NotifyAsync(dto, destinationUserId.ToString(), CancellationToken.None);
                }
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
                    UserId = friendId.ToString(),
                    Status = status,
                    Tags = tags,
                    CustomData = JsonSerializer.Serialize(customData)
                };



            }
            else
            {
                friend = new Friend
                {
                    UserId = friendId.ToString(),
                    Status = FriendConnectionStatus.Disconnected,


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
            var config = await _channel.GetStatusConfig(record.FriendId);

            return CreateFriendDtoDetailed(record.FriendId.ToString(), config, record.Status, record.Tags, record.CustomData);

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
                        return FriendConnectionStatus.Online;
                    default:
                        return FriendConnectionStatus.Disconnected;
                }
            }
        }


        public async Task<bool> IsInFriendList(string userId, string friendId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException("userId");
            }

            if (friendId == null)
            {
                throw new ArgumentNullException("friendId");
            }

            return await _storage.IsUserInMemberListAsync(Guid.Parse(friendId), Guid.Parse(userId));

        }

        public async Task ManageInvitation(User user, string invitationSenderId, bool accept, CancellationToken cancellationToken)
        {
            var ownerId = Guid.Parse(user.Id);
            var senderId = Guid.Parse(invitationSenderId);
            var senderMemberId = new MemberId(ownerId, senderId);
            var destMemberId = new MemberId(senderId, ownerId);

            var senderMember = await _storage.GetListMemberAsync(senderMemberId);
            var destMember = await _storage.GetListMemberAsync(destMemberId);


            var builder = Process(accept);

            await _storage.SaveBatchAsync(builder);
            await NotifyChangesAsync(builder);

            MembersOperationsBuilder Process(bool accept)
            {
                var builder = new MembersOperationsBuilder(senderMember, destMember);
                if (destMember == null || senderMember == null)
                {
                    if (destMember != null && destMember.Status != MemberRecordStatus.Blocked)
                    {
                        builder.Delete(destMember);
                    }

                    if (senderMember != null && senderMember.Status != MemberRecordStatus.Blocked)
                    {
                        builder.Delete(senderMember);
                    }

                    return builder;
                }
                if (destMember.Status != MemberRecordStatus.PendingInvitation)
                {
                    return builder;
                }

                if (accept)
                {

                    return (senderMember) switch
                    {
                        ({ Status: MemberRecordStatus.SentInvitation }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.Accepted)
                            .Update(destMemberId, m => m.Status = MemberRecordStatus.Accepted),
                        ({ Status: MemberRecordStatus.Blocked }) => builder
                            .Update(destMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend),
                        ({ Status: MemberRecordStatus.DeletedByFriend }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.Accepted)
                            .Update(destMemberId, m => m.Status = MemberRecordStatus.Accepted),
                        ({ Status: MemberRecordStatus.PendingInvitation }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.Accepted)
                            .Update(destMemberId, m => m.Status = MemberRecordStatus.Accepted),
                        ({ Status: MemberRecordStatus.Accepted }) => builder
                            .Update(destMemberId, m => m.Status = MemberRecordStatus.Accepted),
                        _ => builder
                    };
                }
                else
                {
                    return (senderMember) switch
                    {
                        ({ Status: MemberRecordStatus.SentInvitation }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend)
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.Blocked }) => builder
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.DeletedByFriend }) => builder
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.PendingInvitation }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend)
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.Accepted }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend)
                            .Delete(destMember),
                        _ => builder
                    };
                }



            }


        }

        public async Task RemoveFriend(User user, string friendId, CancellationToken cancellationToken)
        {



            var friendMemberId = new MemberId(Guid.Parse(user.Id), Guid.Parse(friendId));
            var originMemberId = new MemberId(friendMemberId.ListOwnerId, friendMemberId.UserId);

            var friendMember = await _storage.GetListMemberAsync(friendMemberId);
            var originMember = await _storage.GetListMemberAsync(originMemberId);

            var builder = Process();

            await _storage.SaveBatchAsync(builder);
            await NotifyChangesAsync(builder);

            MembersOperationsBuilder Process()
            {
                var builder = new MembersOperationsBuilder(friendMember, originMember);

                if (originMember == null || friendMember == null)
                {
                    if (friendMember != null)
                    {
                        if (friendMember.Status != MemberRecordStatus.Blocked)
                        {
                            builder.Update(friendMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend);
                        }
                    }
                    if (originMember != null)
                    {
                        builder.Delete(originMember);
                    }
                    return builder;
                }

                builder.Delete(originMember);

                return friendMember switch
                {
                    { Status: MemberRecordStatus.PendingInvitation } => builder.Delete(friendMember),
                    { Status: MemberRecordStatus.Blocked } => builder,
                    _ => builder.Update(friendMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend)
                };

            }

        }

        public async Task SetStatus(User user, FriendListStatusConfig status, string details, CancellationToken cancellationToken)
        {

            UserFriendListConfig newConfig = new() { Status = status, CustomData = JObject.Parse(details) };
            var online = await _channel.UpdateStatusConfig(Guid.Parse(user.Id), newConfig);

            user.UserData["friendsConfiguration"] = JObject.FromObject(newConfig);
            await _users.UpdateUserData(user.Id, user.UserData);

            var records = await _storage.GetListsContainingMemberAsync(Guid.Parse(user.Id), onlyAccepted: true, listType: LIST_TYPE);


            foreach (var record in records)
            {
                var _ = NotifyAsync(new FriendListUpdateDto
                {
                    ItemId = user.Id,
                    Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                    Data = CreateFriendDtoDetailed(user.Id, online ? newConfig : null, MemberRecordStatus.Accepted, record.Tags, record.CustomData)
                }, record.OwnerId.ToString(), cancellationToken);
            }
        }

        public async Task Subscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                var sessions = scope.Resolve<IUserSessions>();
                var session = await sessions.GetSessionById(peer.SessionId, cancellationToken);

                if (session == null || session.User == null)
                {
                    throw new ClientException("NotAuthenticated");
                }

                var user = session.User;


                var statusConfig = GetUserFriendListConfig(user);
                await _channel.AddPeer(Guid.Parse(user.Id), peer, statusConfig);
                var friendsRecords = await _storage.GetListMembersAsync(Guid.Parse(user.Id));
                var friends = new List<Friend>();
                foreach (var record in friendsRecords)
                {
                    friends.Add(await CreateFriendDtoDetailed(record));
                }
                var ctx = new GetFriendsCtx(user.Id, peer.SessionId, friends, false);

                await scope.ResolveAll<IFriendsEventHandler>().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", "An error occurred while executing the friends event handlers", ex); });

                await NotifyAsync(friends.Select(friend => new FriendListUpdateDto { ItemId = friend.UserId, Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }), user.Id, cancellationToken);

                if (!friends.Any())
                {
                    await NotifyAsync(Enumerable.Empty<FriendListUpdateDto>(), user.Id, cancellationToken);
                }
                var newStatus = ComputeStatus(statusConfig, true);
                var userGuid = Guid.Parse(user.Id);
                var owners = await _storage.GetListsContainingMemberAsync(userGuid, true, LIST_TYPE);
                if (newStatus != FriendConnectionStatus.Disconnected)
                {
                    await NotifyAsync(new FriendListUpdateDto { ItemId = userGuid.ToString(), Operation = FriendListUpdateDtoOperation.UpdateStatus, Data = new Friend { Status = newStatus, UserId = userGuid.ToString() } }, owners.Select(m => m.OwnerId.ToString()), cancellationToken);
                }
            }
        }


        public async Task<IEnumerable<Friend>> GetFriends(string userId, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                var friendsRecords = await _storage.GetListMembersAsync(Guid.Parse(userId));
                var friends = new List<Friend>();
                foreach (var record in friendsRecords)
                {
                    friends.Add(await CreateFriendDtoDetailed(record));
                }
                var ctx = new GetFriendsCtx(userId, SessionId.Empty, friends, true);

                await scope.ResolveAll<IFriendsEventHandler>().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", "An error occurred while executing the friends event handlers", ex); });

                return friends;
            }
        }

        public async Task Unsubscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            var (config, userId) = await _channel.RemovePeer(peer.SessionId);
            if (config != null)
            {
                var oldStatus = ComputeStatus(config, true);

                var owners = await _storage.GetListsContainingMemberAsync(userId, true, LIST_TYPE);
                await NotifyAsync(new FriendListUpdateDto { ItemId = userId.ToString(), Operation = FriendListUpdateDtoOperation.UpdateStatus, Data = new Friend { Status = FriendConnectionStatus.Disconnected, UserId = userId.ToString() } }, owners.Select(m => m.OwnerId.ToString()), cancellationToken);

            }
        }



        public async Task<IEnumerable<Friend>> GetFriendsWithStatus(string userId)
        {
            var records = await _storage.GetListMembersAsync(Guid.Parse(userId));

            return await Task.WhenAll(records.Select(r => CreateFriendDtoDetailed(r)));
        }


        private UserFriendListConfig GetUserFriendListConfig(User user)
        {
            if (user.UserData.TryGetValue("friendsConfiguration", out var token))
            {
                return token.ToObject<UserFriendListConfig>() ?? new UserFriendListConfig { CustomData = new JObject(), Status = FriendListStatusConfig.Online };
            }
            else
            {
                return new UserFriendListConfig { CustomData = new JObject(), Status = FriendListStatusConfig.Online };
            }
        }


        public Task NotifyAsync(FriendListUpdateDto data, IEnumerable<string> userIds, CancellationToken cancellationToken) => Notify(Enumerable.Repeat(data, 1), userIds, cancellationToken);
        public Task NotifyAsync(FriendListUpdateDto data, string userId, CancellationToken cancellationToken) => Notify(Enumerable.Repeat(data, 1), Enumerable.Repeat(userId, 1), cancellationToken);
        public Task NotifyAsync(IEnumerable<FriendListUpdateDto> data, string userId, CancellationToken cancellationToken) => Notify(data, Enumerable.Repeat(userId, 1), cancellationToken);

        public async Task Notify(IEnumerable<FriendListUpdateDto> data, IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                var sessionIds = _channel.GetSessionIds(userIds);

                BroadcastToPlayers(sessionIds, "friends.notification", data);
            }
        }

        private void BroadcastToPlayers<T>(IEnumerable<SessionId> peers, string route, T data)
        {
            BroadcastToPlayers(peers, route, (stream, serializer) => { serializer.Serialize(data, stream); });
        }

        private void BroadcastToPlayers(IEnumerable<SessionId> peers, string route, Action<System.IO.Stream, ISerializer> writer)
        {

            _scene.Send(new MatchArrayFilter(peers), route, s => writer(s, _serializer), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);

        }

        public async Task AddNonPersistedFriends(string userId, IEnumerable<Friend> friends, CancellationToken cancellationToken)
        {
            await NotifyAsync(friends.Select(friend => new FriendListUpdateDto { ItemId = friend.UserId, Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }), userId, cancellationToken);
        }



        public async Task Block(string userId, string userIdToBlock, DateTime expiration, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentNullException("userId");
            }

            if (string.IsNullOrWhiteSpace(userIdToBlock))
            {
                throw new ArgumentNullException("userIdToBlock");
            }

            if (userId == userIdToBlock)
            {
                throw new InvalidOperationException("Cannot block himself.");
            }

            var user = await _users.GetUser(userId);
            var userToBlock = await _users.GetUser(userIdToBlock);

            if (user == null)
            {
                throw new ArgumentNullException($"User with UserId {userId} not found");
            }

            if (userToBlock == null)
            {
                throw new ArgumentNullException($"User with UserId {userIdToBlock} not found");
            }
            var userToBlockId = Guid.Parse(userIdToBlock);
            var originId = Guid.Parse(userId);
            var currentOwnerMember = await _storage.GetListMemberAsync(new MemberId(userToBlockId, originId));
            var currentTargetMember = await _storage.GetListMemberAsync(new MemberId(originId, userToBlockId));

            var builder = Process();

            await _storage.SaveBatchAsync(builder);

            await NotifyChangesAsync(builder);
            MembersOperationsBuilder Process()
            {
                var builder = new MembersOperationsBuilder(currentOwnerMember, currentTargetMember);
                if (currentOwnerMember == null)
                {
                    builder.Add(new MemberRecord
                    {
                        CustomData = JsonDocument.Parse("{}"),
                        Expiration = expiration,
                        FriendId = userToBlockId,
                        ListType = LIST_TYPE,
                        OwnerId = originId,
                        Status = MemberRecordStatus.Blocked
                    });
                }
                else
                {
                    builder.Update(new MemberId(userToBlockId, originId), m =>
                    {
                        m.Status = MemberRecordStatus.Blocked;
                        m.Expiration = expiration;
                    });
                }

                if (currentTargetMember != null && currentTargetMember.Status == MemberRecordStatus.Accepted)
                {
                    builder.Update(new MemberId(originId, userToBlockId), m => m.Status = MemberRecordStatus.DeletedByFriend);
                }

                return builder;
            }

        }

        public async Task Unblock(string userId, string userIdToUnblock, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentNullException("userId");
            }

            if (string.IsNullOrWhiteSpace(userIdToUnblock))
            {
                throw new ArgumentNullException("userIdToBlock");
            }

            if (userId == userIdToUnblock)
            {
                throw new InvalidOperationException("Cannot unblock himself.");
            }

            var user = await _users.GetUser(userId);
            var userToUnblock = await _users.GetUser(userIdToUnblock);

            if (user == null)
            {
                throw new ArgumentNullException($"User with UserId {userId} not found");
            }

            if (userToUnblock == null)
            {
                throw new ArgumentNullException($"User with UserId {userIdToUnblock} not found");
            }
            var destId = Guid.Parse(userIdToUnblock);
            var originId = Guid.Parse(userId);
            var currentOwnerMember = await _storage.GetListMemberAsync(new MemberId(destId, originId));
            var currentTargetMember = await _storage.GetListMemberAsync(new MemberId(originId, destId));

            var builder = Process();

            await _storage.SaveBatchAsync(builder);

            await NotifyChangesAsync(builder);
            MembersOperationsBuilder Process()
            {
                var builder = new MembersOperationsBuilder(currentOwnerMember, currentTargetMember);

                if (currentOwnerMember != null)
                {
                    builder.Delete(currentOwnerMember);
                }

                if (currentTargetMember != null && currentTargetMember.Status == MemberRecordStatus.Accepted)
                {
                    builder.Update(new MemberId(originId, destId), m => m.Status = MemberRecordStatus.DeletedByFriend);
                }

                return builder;
            }

        }

        public async Task<Dictionary<string, IEnumerable<string>>> GetBlockedLists(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, IEnumerable<string>>();

            foreach (var userId in userIds)
            {
                result.Add(userId, Enumerable.Empty<string>());
            }
            var batch = userIds.Chunk(100).SelectMany(chunk =>
            {
                var task = GetBlockedListsImpl(chunk, cancellationToken);
                return chunk.Select(userId => (userId, task));
            });

            foreach (var (userId, task) in batch)
            {
                var taskResult = await task;
                foreach (var kvp in taskResult)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        public async Task<Dictionary<string, IEnumerable<string>>> GetBlockedListsImpl(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            if (userIds == null || !userIds.Any())
            {
                return new Dictionary<string, IEnumerable<string>>();
            }

            var members = await _storage.GetListMembersAsync(userIds.Select(u => Guid.Parse(u)), MemberRecordStatus.Blocked);



            var dictionary = new Dictionary<string, IEnumerable<string>>();

            foreach (var member in members)
            {
                if (!dictionary.TryGetValue(member.OwnerId.ToString(), out var list))
                {
                    list = new List<string>();
                }
                ((List<string>)list).Add(member.FriendId.ToString());
            }
            return dictionary;

        }

        public async Task<IEnumerable<string>> GetBlockedList(string userId, CancellationToken cancellationToken)
        {
            var result = await GetBlockedLists(new List<string> { userId }, cancellationToken);
            if (result.TryGetValue(userId, out var blockedUserIds))
            {
                return blockedUserIds;
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        public async Task<MemberDto?> GetRelationship(string userId, string targetUserId, CancellationToken cancellationToken)
        {
            var member = await _storage.GetListMemberAsync(new MemberId(Guid.Parse(targetUserId), Guid.Parse(userId)));
            if (member != null)
            {
                return new MemberDto(member);
            }
            else
            {
                return null;
            }

        }
    }
}
