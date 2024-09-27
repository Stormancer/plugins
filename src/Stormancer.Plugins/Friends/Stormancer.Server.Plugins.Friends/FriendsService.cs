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
using Lucene.Net.Index;
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
using System.Security.Cryptography.Xml;
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
        private readonly Func<IEnumerable<IFriendsEventHandler>> _handlers;
        private readonly ISerializer _serializer;
        private readonly CrossplayService _crossplayService;
        private readonly ILogger _logger;

        public FriendsService(
            FriendsRepository repository,
            MembersStorageService storage,
            IEnvironment environment,
            ISceneHost scene,
            ILogger logger,
            IUserService users,
            IUserSessions sessions,
            Func<IEnumerable<IFriendsEventHandler>> handlers,
            ISerializer serializer,
            CrossplayService crossplayService
            )
        {
            _logger = logger;
            _scene = scene;
            _channel = repository;
            _storage = storage;
            _users = users;
            _sessions = sessions;
            _handlers = handlers;
            _serializer = serializer;
            this._crossplayService = crossplayService;
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

            var builder = new MembersOperationsBuilder(ownerRecord, invitedUserRecord);
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


            await ProcessBuilder(builder);
        }

        public async Task ProcessBuilder(MembersOperationsBuilder builder)
        {
            await _storage.SaveBatchAsync(builder);
            foreach (var operation in builder.Operations)
            {
                Guid destinationUserId = operation.Id.ListOwnerId;
                FriendListUpdateDto? dto;
                switch (operation.Type)
                {


                    case MembersOperationType.Add:
                        {
                            Debug.Assert(operation.Record != null);
                            var record = operation.Record;
                            dto = new FriendListUpdateDto
                            {
                                Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                                Data = await CreateFriendDtoDetailed(record)
                            };
                            var friendsWithInfos = await AddInfos(Enumerable.Repeat(dto.Data, 1));

                            var addingFriendsCtx = new AddingFriendCtx(this, destinationUserId.ToString("N"), friendsWithInfos);



                            await _handlers().RunEventHandler(h => h.OnAddingFriend(addingFriendsCtx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", $"An error occurred while executing {nameof(IFriendsEventHandler.OnAddingFriend)}", ex); });


                            break;
                        }
                    case MembersOperationType.Update:
                        {
                            var record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                            Debug.Assert(record != null);
                            operation.Updater(record);
                            dto = new FriendListUpdateDto
                            {
                                Operation = FriendListUpdateDtoOperation.AddOrUpdate,

                                Data = await CreateFriendDtoDetailed(record)
                            };
                            var friendsWithInfos = await AddInfos(Enumerable.Repeat(dto.Data, 1));

                            var addingFriendsCtx = new AddingFriendCtx(this, destinationUserId.ToString("N"), friendsWithInfos);



                            await _handlers().RunEventHandler(h => h.OnAddingFriend(addingFriendsCtx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", $"An error occurred while executing {nameof(IFriendsEventHandler.OnAddingFriend)}", ex); });

                            break;
                        }
                    case MembersOperationType.Delete:
                        {
                            var record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                            Debug.Assert(record != null);
                            dto = new FriendListUpdateDto
                            {
                                Operation = FriendListUpdateDtoOperation.Remove,

                                Data = new Friend
                                {
                                    Status = new Dictionary<string, FriendConnectionStatus> { [Users.Constants.PROVIDER_TYPE_STORMANCER] = FriendConnectionStatus.Disconnected },
                                    UserIds = [
                                        new (Users.Constants.PROVIDER_TYPE_STORMANCER,  operation.Id.UserId.ToString("N"))
                                    ]
                                }
                            };

                            break;
                        }
                    default:
                        dto = null;
                        break;
                }


                if (dto != null)
                {
                    _channel.ApplyFriendListUpdate(destinationUserId, dto);

                }
            }
        }


        [Obsolete]
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

                            Data = await CreateFriendDtoDetailed(record)
                        };
                        break;
                    case MembersOperationType.Delete:
                        record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                        Debug.Assert(record != null);
                        dto = new FriendListUpdateDto
                        {
                            Operation = FriendListUpdateDtoOperation.Remove,

                            Data = new Friend
                            {
                                Status = new Dictionary<string, FriendConnectionStatus> { [Users.Constants.PROVIDER_TYPE_STORMANCER] = FriendConnectionStatus.Disconnected },
                                UserIds = [
                                    new (Users.Constants.PROVIDER_TYPE_STORMANCER,  operation.Id.UserId.ToString("N"))
                                ]
                            }
                        };

                        break;
                    default:
                        dto = null;
                        break;
                }
                if (dto != null)
                {
                    await NotifyAsync(dto, destinationUserId.ToString("N"), CancellationToken.None);
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
            var config = await _channel.GetStatusConfig(new PlatformId(Users.Constants.PROVIDER_TYPE_STORMANCER, record.FriendId.ToString("N")));

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


            await ProcessBuilder(builder);

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
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.Blocked }) => builder
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.DeletedByFriend }) => builder
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.PendingInvitation }) => builder
                            .Delete(senderMember)
                            .Delete(destMember),
                        ({ Status: MemberRecordStatus.Accepted }) => builder
                            .Update(senderMemberId, m => m.Status = MemberRecordStatus.DeletedByFriend)
                            .Delete(destMember),
                        _ => builder
                    };
                }



            }


        }

        public async Task RemoveFriend(User user, User friend, CancellationToken cancellationToken)
        {



            var friendMemberId = new MemberId(Guid.Parse(user.Id), Guid.Parse(friend.Id));
            var originMemberId = new MemberId(friendMemberId.ListOwnerId, friendMemberId.UserId);

            var friendMember = await _storage.GetListMemberAsync(friendMemberId);
            var originMember = await _storage.GetListMemberAsync(originMemberId);

            var builder = Process();


            await ProcessBuilder(builder);

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

                    Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                    Data = CreateFriendDtoDetailed(user.Id, online ? newConfig : null, MemberRecordStatus.Accepted, record.Tags, record.CustomData)
                }, record.OwnerId.ToString("N"), cancellationToken);
            }
        }

        public async Task Subscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        { 

            var session = await _sessions.GetSessionById(peer.SessionId, cancellationToken);

            if (session == null || session.User == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            var user = session.User;


            var statusConfig = GetUserFriendListConfig(user);
            await _channel.AddPeer(Guid.Parse(user.Id), peer, user, statusConfig);
            var friendsRecords = await _storage.GetListMembersAsync(Guid.Parse(user.Id));
            var friends = new List<Friend>();
            foreach (var record in friendsRecords)
            {
                friends.Add(await CreateFriendDtoDetailed(record));
            }
            var ctx = new GetFriendsCtx(this, user.Id, peer.SessionId, friends, false);

            await _handlers().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", $"An error occurred while executing {nameof(IFriendsEventHandler.OnGetFriends)}", ex); });

            var friendsWithInfos = await AddInfos(friends);

            var addingFriendsCtx = new AddingFriendCtx(this, user.Id, friendsWithInfos);



            await _handlers().RunEventHandler(h => h.OnAddingFriend(addingFriendsCtx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", $"An error occurred while executing {nameof(IFriendsEventHandler.OnAddingFriend)}", ex); });


            if (!_crossplayService.IsCrossplayEnabled(user))
            {

                var platform = session.platformId.Platform;

                var friendsToDelete = new List<Friend>();
                foreach (var friend in friendsWithInfos)
                {
                    //Remove user on other platforms if the user has cross play disabled.

                    if (!friend.friend.UserIds.Any(p => p.Platform == platform))
                    {
                        friendsToDelete.Add(friend.friend);
                    }
                    //If friend is on the same platform, but as cross play enabled, set them as disconnected.
                    else if (_crossplayService.IsCrossplayEnabled(user))
                    {
                        friend.friend.Status["stormancer"] = FriendConnectionStatus.Disconnected;
                    }
                }

                foreach (var f in friendsToDelete)
                {
                    friends.Remove(f);
                }

            }
            else
            {
                foreach (var friend in friendsWithInfos)
                {
                    if (!_crossplayService.IsCrossplayEnabled(user))
                    {
                        friend.friend.Status["stormancer"] = FriendConnectionStatus.Disconnected;
                    }
                }
            }


            _channel.ApplyFriendListUpdates(Guid.Parse(user.Id), friends.Select(friend => new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }));
            //await NotifyAsync(friends.Select(friend => new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }), user.Id, cancellationToken);


            var newStatus = ComputeStatus(statusConfig, true);
            var userGuid = Guid.Parse(user.Id);

            if (newStatus != FriendConnectionStatus.Disconnected)
            {

                await foreach (var (ownerId, owner, friend) in _channel.GetListsContainingMemberAsync(new PlatformId(Users.Constants.PROVIDER_TYPE_STORMANCER, user.Id)))
                {

                    var updatingFriendCtx = new UpdatingFriendStatusCtx(this, ownerId, friend, newStatus, user, session);
                    var customData = friend.CustomData;
                    await _handlers().RunEventHandler(h => h.OnUpdatingStatus(updatingFriendCtx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", $"An error occurred while executing {nameof(IFriendsEventHandler.OnUpdatingStatus)}", ex); });

                    if (AreCrossPlayOptionsEqual(owner, user))
                    {
                        friend.Status["stormancer"] = updatingFriendCtx.NewStatus;
                    }
                    else
                    {
                        friend.Status["stormancer"] = FriendConnectionStatus.Disconnected;
                    }
                    _channel.ApplyFriendListUpdate(ownerId, new FriendListUpdateDto { Data = friend, Operation = customData != friend.CustomData ? FriendListUpdateDtoOperation.AddOrUpdate : FriendListUpdateDtoOperation.UpdateStatus });
                }

            }

        }
        private bool AreCrossPlayOptionsEqual(User user1, User user2)
        {
            var o1 = _crossplayService.IsCrossplayEnabled(user1);
            var o2 = _crossplayService.IsCrossplayEnabled(user2);
            return o1 == o2;
        }

        private async Task<List<(UserSessionInfos? userInfos, Friend friend)>> AddInfos(IEnumerable<Friend> friends)
        {
            var list = new List<(UserSessionInfos? infos, Friend friend)>();
            var ids = friends.Select(f => (f.UserIds.FirstOrDefault(), f));

            var result = await _sessions.GetDetailedUserInformationAsync(friends.Select(f => f.UserIds.FirstOrDefault()), CancellationToken.None);

            foreach (var (platformId, f) in ids)
            {

                var friendUId = f.UserIds.FirstOrDefault();

                if (!friendUId.IsUnknown && result.TryGetValue(friendUId, out var info))
                {
                    if (info.UserId != null)
                    {
                        var pid = new PlatformId { Platform = Users.Constants.PROVIDER_TYPE_STORMANCER, PlatformUserId = info.UserId };
                        if (!f.UserIds.Contains(pid))
                        {
                            f.UserIds.Add(pid);
                        }
                        //if (!f.Tags.Contains("friends.blocked"))
                        //{
                        f.Status[Users.Constants.PROVIDER_TYPE_STORMANCER] = await GetStatusAsync(pid, CancellationToken.None);
                        //}
                    }
                    list.Add((info, f));
                }
                else
                {
                    list.Add((null, f));
                }


            }
            return list;
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
                var ctx = new GetFriendsCtx(this, userId, SessionId.Empty, friends, true);

                await scope.ResolveAll<IFriendsEventHandler>().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", "An error occurred while executing the friends event handlers", ex); });

                return friends;
            }
        }

        public async Task RefreshSubscription(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            await _channel.RemovePeer(peer.SessionId);
            await Subscribe(peer, cancellationToken);
        }

        public async Task Unsubscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            var (config, userId) = await _channel.RemovePeer(peer.SessionId);
            if (config != null)
            {
                var oldStatus = ComputeStatus(config, true);

                var owners = await _storage.GetListsContainingMemberAsync(userId, true, LIST_TYPE);
                await NotifyAsync(new FriendListUpdateDto
                {
                    Operation = FriendListUpdateDtoOperation.UpdateStatus,
                    Data = new Friend
                    {
                        Status = new Dictionary<string, FriendConnectionStatus> { [Users.Constants.PROVIDER_TYPE_STORMANCER] = FriendConnectionStatus.Disconnected },
                        UserIds = new() {
                            new PlatformId {
                                PlatformUserId = userId.ToString("N"), Platform = Stormancer.Server.Plugins.Users.Constants.PROVIDER_TYPE_STORMANCER
                            }
                        }
                    }
                }, owners.Select(m => m.OwnerId.ToString("N")), cancellationToken);

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
            await NotifyAsync(friends.Select(friend => new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }), userId, cancellationToken);
        }



        public async Task Block(User user, User userToBlock, DateTime expiration, CancellationToken cancellationToken)
        {


            if (user.Id == userToBlock.Id)
            {
                throw new ClientException("social.blockList.addFailed?cannotBlockYourself");
            }


            var userToBlockId = Guid.Parse(userToBlock.Id);
            var originId = Guid.Parse(user.Id);
            var currentOwnerMember = await _storage.GetListMemberAsync(new MemberId(userToBlockId, originId));
            var currentTargetMember = await _storage.GetListMemberAsync(new MemberId(originId, userToBlockId));

            var builder = Process();


            await ProcessBuilder(builder);

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

        public async Task Unblock(User user, User userToUnblock, CancellationToken cancellationToken)
        {


            if (user.Id == userToUnblock.Id)
            {
                throw new InvalidOperationException("Cannot unblock himself.");
            }

            var destId = Guid.Parse(userToUnblock.Id);
            var originId = Guid.Parse(user.Id);
            var currentOwnerMember = await _storage.GetListMemberAsync(new MemberId(destId, originId));
            var currentTargetMember = await _storage.GetListMemberAsync(new MemberId(originId, destId));

            var builder = Process();

            await ProcessBuilder(builder);

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
                result[userId] = Enumerable.Empty<string>();
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
            var result = new Dictionary<string, IEnumerable<string>>();
            if (userIds == null || !userIds.Any())
            {
                return result;
            }

            var offlineUsers = new List<string>();
            foreach (var userId in userIds)
            {
                if (_channel.TryGetBlockList(Guid.Parse(userId), out var blockList)) //User online
                {
                    result[userId] = blockList;
                }
                else if(!offlineUsers.Contains(userId)) //If offline, get from DB
                {
                    offlineUsers.Add(userId);
                }
            }


            var members = await _storage.GetListMembersAsync(offlineUsers.Select(u => Guid.Parse(u)), MemberRecordStatus.Blocked);


            foreach (var userId in offlineUsers)
            {
                result[userId] = new List<string>();
            }

            foreach (var member in members)
            {
                if (result.TryGetValue(member.OwnerId.ToString("N"), out var e) && e is List<string> list)
                {
                    list.Add(member.FriendId.ToString("N"));
                }


            }
            return result;

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

        public async Task ProcessUpdates(string userId, IEnumerable<FriendListUpdateDto> updates)
        {
            var friends = updates.Where(dto => dto.Operation == FriendListUpdateDtoOperation.AddOrUpdate).Select(dto => dto.Data);
            if (friends.Any())
            {
                var list = new List<(UserSessionInfos? infos, Friend friend)>();
                var ids = friends.Select(f => (f.UserIds.FirstOrDefault(), f));

                var result = await _sessions.GetDetailedUserInformationAsync(ids.Select(t => t.Item1), CancellationToken.None);



                foreach (var (platformId, f) in ids)
                {
                    var friendUId = f.UserIds.FirstOrDefault();
                    if (!friendUId.IsUnknown && result.TryGetValue(friendUId, out var info))
                    {
                        if (info.UserId != null)
                        {
                            var pid = new PlatformId { Platform = Users.Constants.PROVIDER_TYPE_STORMANCER, PlatformUserId = info.UserId };
                            if (!f.UserIds.Contains(pid))
                            {
                                f.UserIds.Add(pid);
                            }

                            f.Status[Users.Constants.PROVIDER_TYPE_STORMANCER] = await GetStatusAsync(pid, CancellationToken.None);

                        }
                        list.Add((info, f));
                    }
                    else
                    {
                        list.Add((null, f));
                    }


                }

                var ctx = new AddingFriendCtx(this, userId, list);
                await _handlers().RunEventHandler(h => h.OnAddingFriend(ctx), ex => _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", $"An error occurred while executing {nameof(IFriendsEventHandler.OnAddingFriend)}", ex));
            }



            _channel.ApplyFriendListUpdates(Guid.Parse(userId), updates);

        }

        public async Task<FriendConnectionStatus> GetStatusAsync(PlatformId userId, CancellationToken cancellationToken)
        {

            var friendConfig = await _channel.GetStatusConfig(userId);

            if (friendConfig == null) //Friend not connected.
            {
                return FriendConnectionStatus.Disconnected;
            }
            else //Friend connected
            {
                return ComputeStatus(friendConfig, true);
            }

        }
    }
}
