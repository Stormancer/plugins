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

using Nest;
using Stormancer.Core;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    internal class FriendsService : IFriendsService
    {
        private const string ROLE_BLOCKED = "blocked";

        private const string INDEX_NAME = "friends";
        private readonly FriendsRepository _channel;
        private readonly ISceneHost _scene;
        private readonly IUserService _users;

        private readonly ILogger _logger;
        private readonly IESClientFactory _esClient;

        private static bool _indexExistsChecked = false;
        private static AsyncLock _mappingCheckedLock = new AsyncLock();

        public FriendsService(
            FriendsRepository repository,
            IESClientFactory clientFactory,
            IEnvironment environment,
            ISceneHost scene,
            ILogger logger,
            IUserService users
            )
        {
            _logger = logger;
            _scene = scene;
            _channel = repository;
            _users = users;
            _esClient = clientFactory;
        }

        private async Task<Nest.IElasticClient> CreateClient<T>(object[] parameters = null)
        {
            var client = await _esClient.CreateClient<T>(INDEX_NAME, parameters);
            if (!_indexExistsChecked)
            {
                using (await _mappingCheckedLock.LockAsync())
                {
                    if (!_indexExistsChecked)
                    {
                        _indexExistsChecked = true;
                        await CreateFriendsIndex();
                    }
                }
            }
            return client;
        }

        private async Task CreateFriendsIndex()
        {
            var client = await CreateClient<MemberRecord>();
            await client.Indices.CreateAsync(client.ConnectionSettings.DefaultIndex);
            await client.MapAsync<MemberRecord>(m => m
            .Properties(pd => pd
                .Keyword(kpd => kpd.Name(m => m.OwnerId))
                .Keyword(kpd => kpd.Name(m => m.FriendId))
                ));
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

            var friendRecord = new MemberRecord { FriendId = user.Id, OwnerId = friend.Id, Status = FriendInvitationStatus.WaitingAccept };

            var client = await CreateClient<MemberRecord>();

            var result = await client.IndexAsync(friendRecord, desc => desc.Routing(friendRecord.OwnerId));

            if (!result.IsValid)
            {
                throw new InvalidOperationException("Failed to invite friend.", result.OriginalException);
            }

            await Notify(Enumerable.Repeat(new FriendListUpdateDto
            {
                ItemId = user.Id,
                Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                Data = CreateFriendDtoSummary(friendRecord)
            }, 1), friend.Id, cancellationToken);
        }

        private Friend CreateFriendDtoSummary(MemberRecord record)
        {
            var friend = new Friend { UserId = record.FriendId, Status = FriendStatus.Pending };
            return friend;
        }

        private async Task<Friend> CreateFriendDtoDetailed(MemberRecord record)
        {
            var config = await _channel.GetStatusConfig(record.FriendId);
            var friend = new Friend { UserId = record.FriendId, Tags = record.Tags };
            if (config == null)//Not connected
            {
                friend.Status = FriendStatus.Disconnected;
                config = await GetStatusConfig(friend.UserId);
            }
            else //Connected
            {
                if (record.Status == FriendInvitationStatus.Accepted)
                {
                    switch (config.Status)
                    {
                        case FriendListStatusConfig.Invisible:
                            friend.Status = FriendStatus.Disconnected;
                            break;
                        case FriendListStatusConfig.Away:
                            friend.Status = FriendStatus.Away;
                            break;
                        case FriendListStatusConfig.Online:
                            friend.Status = FriendStatus.Online;
                            break;
                        default:
                            break;
                    }
                }
                else if (record.Status == FriendInvitationStatus.WaitingAccept)
                {
                    friend.Status = FriendStatus.Pending;
                }
                else
                {
                    friend.Status = FriendStatus.Disconnected;
                }
            }

            friend.CustomData["Details"] = config.CustomData ?? "";
            friend.LastConnected = config.LastConnected == DateTime.MinValue ? DateTimeOffset.UnixEpoch : config.LastConnected;

            return friend;
        }

        private DateTime Last(DateTime date1, DateTime date2)
        {
            if (date1 > date2)
            {
                return date1;
            }
            else
            {
                return date2;
            }
        }

        private FriendStatus ComputeStatus(FriendListConfigRecord config, bool online)
        {
            if (!online)
            {
                return FriendStatus.Disconnected;
            }
            else
            {
                switch (config.Status)
                {
                    case FriendListStatusConfig.Invisible:
                        return FriendStatus.Disconnected;
                    case FriendListStatusConfig.Away:
                        return FriendStatus.Away;
                    case FriendListStatusConfig.Online:
                        return FriendStatus.Online;
                    default:
                        return FriendStatus.Disconnected;
                }
            }
        }

        private Friend CreateFriendDtoDetailed(FriendListConfigRecord config, User user, bool online = true)
        {
            return new Friend { UserId = user.Id, LastConnected = user.LastLogin, CustomData = { { "Details", config.CustomData ?? "" } }, Status = ComputeStatus(config, online) };
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

            var client = await CreateClient<MemberRecord>();

            var result = await client.DocumentExistsAsync<MemberRecord>(userId + "_" + friendId, desc => desc.Routing(userId));

            if (result.ServerError == null)
            {
                return result.Exists;
            }
            else
            {
                throw new InvalidOperationException($"An error occured while searching friend {friendId} for user {userId}", result.OriginalException);
            }
        }

        public async Task ManageInvitation(User user, string senderId, bool accept, CancellationToken cancellationToken)
        {
            var client = await CreateClient<MemberRecord>();

            var r = await client.GetAsync<MemberRecord>(user.Id + "_" + senderId, desc => desc.Routing(user.Id));
            if (!r.Found)
            {
                return;
            }
            var targetFriendRecord = r.Source;

            if (!accept)
            {
                await client.DeleteAsync<MemberRecord>(targetFriendRecord.Id, desc => desc.Routing(user.Id));
                await Notify(Enumerable.Repeat(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.Remove, ItemId = senderId, Data = new Friend { } }, 1), user.Id, cancellationToken);
            }
            else
            {
                targetFriendRecord.Status = FriendInvitationStatus.Accepted;
                await client.UpdateAsync<MemberRecord>(user.Id + "_" + senderId, desc => desc.Routing(user.Id).Doc(targetFriendRecord));
                var senderFriendRecord = new MemberRecord { FriendId = user.Id, OwnerId = senderId, Status = FriendInvitationStatus.Accepted };

                await client.IndexAsync<MemberRecord>(senderFriendRecord, desc => desc.Routing(senderId));
                await Notify(Enumerable.Repeat(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.AddOrUpdate, ItemId = senderId, Data = await CreateFriendDtoDetailed(targetFriendRecord) }, 1), user.Id, cancellationToken);
                await Notify(Enumerable.Repeat(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.AddOrUpdate, ItemId = user.Id, Data = await CreateFriendDtoDetailed(senderFriendRecord) }, 1), senderId, cancellationToken);
            }
        }

        public async Task RemoveFriend(User user, string friendId, CancellationToken cancellationToken)
        {
            var client = await CreateClient<MemberRecord>();
            await client.DeleteAsync<MemberRecord>(user.Id + "_" + friendId, desc => desc.Routing(user.Id));
            await client.DeleteAsync<MemberRecord>(friendId + "_" + user.Id, desc => desc.Routing(friendId));
            //var r = await client.UpdateAsync<object>(friendId + "_" + user.Id, desc => desc.Doc(new { Status = FriendRecordStatus.RemovedByFriend }).FilterPath(new[] { "status" }).DocAsUpsert(false).Routing(friendId));

            await Notify(Enumerable.Repeat(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.UpdateStatus, ItemId = user.Id, Data = new Friend { Status = FriendStatus.Disconnected } }, 1), friendId, cancellationToken);
            await Notify(Enumerable.Repeat(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.Remove, ItemId = friendId, Data = new Friend { } }, 1), user.Id, cancellationToken);
        }

        public async Task SetStatus(User user, FriendListStatusConfig status, string details, CancellationToken cancellationToken)
        {
            var client = await CreateClient<FriendListConfigRecord>();

            FriendListConfigRecord config = await _channel.UpdateStatusConfig(user.Id, status, details);

            var online = config != null;

            if (!online)
            {
                var r = await client.GetAsync<FriendListConfigRecord>(user.Id);
                if (r.Found)
                {
                    config = r.Source;
                }
                else
                {
                    config = new FriendListConfigRecord { Id = user.Id, Status = status, LastConnected = DateTime.UtcNow, CustomData = details };
                }
            }

            await client.UpdateAsync<FriendListConfigRecord>(config.Id, s => s.DocAsUpsert().Doc(config));

            var friends = await GetFriendRecords(user.Id);

            var _ = Notify(Enumerable.Repeat(new FriendListUpdateDto
            {
                ItemId = user.Id,
                Operation = FriendListUpdateDtoOperation.AddOrUpdate,
                Data = CreateFriendDtoDetailed(config, user, online)
            }, 1), friends.Where(f => f.Status == FriendInvitationStatus.Accepted).Select(f => f.FriendId), cancellationToken);
        }

        public async Task Subscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                var sessions = scope.Resolve<IUserSessions>();
                var session = await sessions.GetSessionById(peer.SessionId, cancellationToken);

                if (session == null)
                {
                    throw new ClientException("NotAuthenticated");
                }

                var user = session.User;

                var statusConfig = await GetStatusConfig(user.Id);
                await _channel.AddPeer(user.Id, peer, statusConfig);
                var friendsRecords = await GetFriendRecords(user.Id);
                var friends = new List<Friend>();
                foreach (var record in friendsRecords)
                {
                    friends.Add(await CreateFriendDtoDetailed(record));
                }
                var ctx = new GetFriendsCtx(user.Id, friends, false);

                await scope.ResolveAll<IFriendsEventHandler>().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", "An error occured while executing the friends event handlers", ex); });

                await Notify(friends.Select(friend => new FriendListUpdateDto { ItemId = friend.UserId, Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }), user.Id, cancellationToken);

                if (!friends.Any())
                {
                    await Notify(Enumerable.Empty<FriendListUpdateDto>(), user.Id, cancellationToken);
                }
                var newStatus = ComputeStatus(statusConfig, true);
                if (newStatus == FriendStatus.Online)
                {
                    await Notify(Enumerable.Repeat(new FriendListUpdateDto { ItemId = user.Id, Operation = FriendListUpdateDtoOperation.UpdateStatus, Data = new Friend { Status = newStatus } }, 1), friendsRecords.Select(f => f.FriendId), cancellationToken);
                }
            }
        }

        public async Task<IEnumerable<Friend>> GetFriends(string userId, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                var friendsRecords = await GetFriendRecords(userId);
                var friends = new List<Friend>();
                foreach (var record in friendsRecords)
                {
                    friends.Add(await CreateFriendDtoDetailed(record));
                }
                var ctx = new GetFriendsCtx(userId, friends, true);

                await scope.ResolveAll<IFriendsEventHandler>().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(Diagnostics.LogLevel.Warn, "FriendsEventHandlers", "An error occured while executing the friends event handlers", ex); });

                return friends;
            }
        }

        public async Task Unsubscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            var config = await _channel.RemovePeer(peer.SessionId);
            if (config != null && config.Item1 != null)
            {
                var oldStatus = ComputeStatus(config.Item1, true);
                if (oldStatus != FriendStatus.Disconnected)
                {
                    var friends = await GetFriendRecords(config.Item2);
                    await Notify(Enumerable.Repeat(new FriendListUpdateDto { ItemId = config.Item2, Operation = FriendListUpdateDtoOperation.UpdateStatus, Data = new Friend { Status = FriendStatus.Disconnected } }, 1), friends.Select(f => f.FriendId).ToArray(), cancellationToken);
                }
            }
        }

        private async Task<MemberRecord> GetFriend(string userId, string friendId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException("userId");
            }

            if (friendId == null)
            {
                throw new ArgumentNullException("friendId");
            }

            var client = await CreateClient<MemberRecord>();

            var result = await client.GetAsync<MemberRecord>(userId + "_" + friendId, desc => desc.Routing(userId));

            if (result.IsValid || !result.Found)
            {
                return result.Source;
            }
            else
            {
                throw new InvalidOperationException($"An error occured while searching friend {friendId} for user {userId}", result.OriginalException);
            }
        }

        public async Task<IEnumerable<Friend>> GetFriendsWithStatus(string userId)
        {
            var records = await GetFriendRecords(userId);

            return await Task.WhenAll(records.Select(r => CreateFriendDtoDetailed(r)));
        }

        private async Task<IEnumerable<MemberRecord>> GetFriendRecords(string userId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException("userId");
            }
            var client = await CreateClient<MemberRecord>();
            var result = await client.SearchAsync<MemberRecord>(rq => rq.Query(
                q => q.Term(t => t.Field("ownerId.keyword")
                .Value(userId)))
                .Routing(userId)
                .AllowNoIndices());

            if (result.IsValid)
            {
                return result.Documents;
            }
            else if (result.ServerError == null || result.ServerError.Status == 404)
            {
                return Enumerable.Empty<MemberRecord>();
            }
            else
            {
                _logger.Log(Diagnostics.LogLevel.Error, "FriendsService.GetFriends", "an error occurred when trying to retrieve friends",
                new
                {
                    result.ApiCall.HttpMethod,
                    result.ApiCall.Uri,
                    result.ApiCall.DebugInformation,
                    result.ApiCall.HttpStatusCode,
                    requestBody = result.ApiCall.RequestBodyInBytes != null ? System.Text.Encoding.UTF8.GetString(result.ApiCall.RequestBodyInBytes) : null,
                    responseBody = result.ApiCall.ResponseBodyInBytes != null ? System.Text.Encoding.UTF8.GetString(result.ApiCall.ResponseBodyInBytes) : null
                });
                throw new InvalidOperationException("An error occured while searching friends", result.OriginalException);
            }
        }

        private async Task<FriendListConfigRecord> GetStatusConfig(string userId)
        {
            var client = await CreateClient<FriendListConfigRecord>();

            var result = await client.GetAsync<FriendListConfigRecord>(userId);

            if (result.Found)
            {
                return result.Source ?? new FriendListConfigRecord { Id = userId, Status = FriendListStatusConfig.Online, CustomData = null };
            }
            else
            {
                return new FriendListConfigRecord { Id = userId, Status = FriendListStatusConfig.Online, CustomData = null };
            }
        }

        public Task Notify(IEnumerable<FriendListUpdateDto> data, string userId, CancellationToken cancellationToken) => Notify(data, Enumerable.Repeat(userId, 1), cancellationToken);

        public async Task Notify(IEnumerable<FriendListUpdateDto> data, IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                var peers = (await Task.WhenAll(userIds.Select(key => sessions.GetPeer(key, cancellationToken)))).Where(p => p != null);

                BroadcastToPlayers(peers!, "friends.notification", data);
            }
        }

        private void BroadcastToPlayers<T>(IEnumerable<IScenePeerClient> peers, string route, T data)
        {
            BroadcastToPlayers(peers, route, (stream, serializer) => { serializer.Serialize(data, stream); });
        }

        private void BroadcastToPlayers(IEnumerable<IScenePeerClient> peers, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            foreach (var group in peers.Where(p => p != null).GroupBy(p => p.Serializer()))
            {
                _scene.Send(new MatchArrayFilter(group), route, s => writer(s, group.Key), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
            }
        }

        public async Task AddNonPersistedFriends(string userId, IEnumerable<Friend> friends, CancellationToken cancellationToken)
        {
            await Notify(friends.Select(friend => new FriendListUpdateDto { ItemId = friend.UserId, Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = friend }), userId, cancellationToken);
        }

        public async Task<MemberDto?> GetRelationship(string userId, string targetUserId, CancellationToken cancellationToken)
        {
            var client = await CreateClient<MemberRecord>();

            var result = await client.GetAsync<MemberRecord>(userId + "_" + targetUserId, desc => desc.Routing(userId));

            if (result.ServerError == null)
            {
                return (result.Source != null ? new MemberDto(result.Source) : null);
            }
            else
            {
                throw new InvalidOperationException($"An error occured while searching relationship with {targetUserId} for user {userId}", result.OriginalException);
            }
        }

        public async Task Block(string userId, string userIdToBlock, CancellationToken cancellationToken)
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

            var client = await CreateClient<MemberRecord>();

            var result = await client.GetAsync<MemberRecord>(userId + "_" + userIdToBlock, desc => desc.Routing(userId));

            if (result.ServerError != null)
            {
                throw new InvalidOperationException($"An error occured while searching relationship with {userIdToBlock} for user {userId}", result.OriginalException);
            }

            MemberRecord? memberRecord = (result.Found ? result.Source : null);

            bool createRecord = false;
            if (memberRecord == null)
            {
                memberRecord = new MemberRecord();
                createRecord = true;
            }

            memberRecord.OwnerId = userId;
            memberRecord.FriendId = userIdToBlock;
            memberRecord.Status = FriendInvitationStatus.Unknow;
            memberRecord.Roles = new List<string> { ROLE_BLOCKED };
            memberRecord.Tags = new List<string>();

            if (createRecord)
            {
                await client.IndexAsync(memberRecord, desc => desc.Routing(userId).Refresh(Elasticsearch.Net.Refresh.WaitFor));
            }
            else
            {
                await client.UpdateAsync<MemberRecord>(userId + "_" + userIdToBlock, desc => desc.Routing(userId).Doc(memberRecord).Refresh(Elasticsearch.Net.Refresh.WaitFor));
            }

            await Notify(Enumerable.Repeat(new FriendListUpdateDto { ItemId = userId, Operation = FriendListUpdateDtoOperation.AddOrUpdate, Data = new Friend { UserId = userIdToBlock, Status = FriendStatus.Unknow, Roles = new List<string> { ROLE_BLOCKED } } }, 1), userId, cancellationToken);
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

            var client = await CreateClient<MemberRecord>();

            var result = await client.DeleteAsync<MemberRecord>(userId + "_" + userIdToUnblock, desc => desc.Routing(userId).Refresh(Elasticsearch.Net.Refresh.WaitFor));

            if (result.IsValid)
            {
                await Notify(Enumerable.Repeat(new FriendListUpdateDto { ItemId = userId, Operation = FriendListUpdateDtoOperation.Remove, Data = new Friend { UserId = userIdToUnblock } }, 1), userId, cancellationToken);
            }
            else if (result.ServerError != null)
            {
                throw new InvalidOperationException($"An error occured while deleting relationship with {userIdToUnblock} for user {userId}", result.OriginalException);
            }
        }

        public async Task<Dictionary<string, IEnumerable<string>>> GetBlockedLists(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, IEnumerable<string>>();

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

            var client = await CreateClient<MemberRecord>();
            var result = await client.MultiSearchAsync(null, rq =>
            {
                foreach (var userId in userIds)
                {
                    rq = rq.Search<MemberRecord>(s => s
                        .Query(
                            q => q.Bool(bq => bq
                            .Must(
                                qcd => qcd.Term(m => m.OwnerId, userId),
                                qcd => qcd.Term(m => m.Roles, ROLE_BLOCKED)
                                )
                            )
                        )
                    );
                }
                return rq;
            }, cancellationToken);

            if (result.IsValid)
            {
                var dictionary = new Dictionary<string, IEnumerable<string>>();
                var responses = result.GetResponses<MemberRecord>();

                foreach (var (userId, response) in userIds.Zip(responses))
                {
                    dictionary[userId] = response.Documents.Select(doc => doc.FriendId);
                }
                return dictionary;
            }
            else if (result.ServerError == null || result.ServerError.Status == 404)
            {
                return new Dictionary<string, IEnumerable<string>>();
            }
            else
            {
                _logger.Log(Diagnostics.LogLevel.Error, "FriendsService.GetBlockedList", "an error occurred when trying to retrieve blocked users",
                new
                {
                    result.ApiCall.HttpMethod,
                    result.ApiCall.Uri,
                    result.ApiCall.DebugInformation,
                    result.ApiCall.HttpStatusCode,
                    requestBody = result.ApiCall.RequestBodyInBytes != null ? System.Text.Encoding.UTF8.GetString(result.ApiCall.RequestBodyInBytes) : null,
                    responseBody = result.ApiCall.ResponseBodyInBytes != null ? System.Text.Encoding.UTF8.GetString(result.ApiCall.ResponseBodyInBytes) : null
                });
                throw new InvalidOperationException("An error occured while searching friends", result.OriginalException);
            }
        }

        public async Task<IEnumerable<string>> GetBlockedList(string userId, CancellationToken cancellationToken)
        {
            var result = await GetBlockedLists(new List<string> { userId }, cancellationToken);
            if (result.TryGetValue(userId, out var blockedUserIds))
            {
                return blockedUserIds;
            }
            throw new InvalidOperationException("Invalid UserId");
        }
    }
}
