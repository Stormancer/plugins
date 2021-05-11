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

using Stormancer.Server.Plugins.API;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Stormancer.Server.Plugins.Friends
{
    internal class FriendsService : IFriendsService
    {
        private const string INDEX_NAME = "friends";
        private readonly FriendsRepository _channel;
        private readonly ISceneHost _scene;
        private readonly IUserService _users;

        private readonly ILogger _logger;
        private readonly IESClientFactory _esClient;

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
            return await _esClient.CreateClient<T>(INDEX_NAME, parameters);
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

            var friendRecord = new MemberRecord { FriendId = user.Id, OwnerId = friend.Id, Status = FriendRecordStatus.WaitingAccept };

            var client = await CreateClient<MemberRecord>();

            var result = await client.IndexAsync(friendRecord, desc => desc.Routing(friendRecord.OwnerId));

            if (!result.IsValid)
            {
                throw new InvalidOperationException("Failed to invite friend.", result.OriginalException);
            }

            await Notify(new FriendListUpdateDto
            {
                ItemId = user.Id,
                Operation = FriendListUpdateDtoOperation.Add,
                Data = CreateFriendDtoSummary(friendRecord)
            }, friend.Id, cancellationToken);
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
                if (record.Status == FriendRecordStatus.Accepted)
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
                else if (record.Status == FriendRecordStatus.WaitingAccept)
                {
                    friend.Status = FriendStatus.Pending;
                }
                else
                {
                    friend.Status = FriendStatus.Disconnected;
                }
            }

            friend.Details = config.CustomData;
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
            return new Friend { UserId = user.Id, LastConnected = user.LastLogin, Details = config.CustomData, Status = ComputeStatus(config, online) };
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
                await Notify(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.Remove, ItemId = senderId, Data = new Friend { } }, user.Id,cancellationToken);
            }
            else
            {
                targetFriendRecord.Status = FriendRecordStatus.Accepted;
                await client.UpdateAsync<MemberRecord>(user.Id + "_" + senderId, desc => desc.Routing(user.Id).Doc(targetFriendRecord));
                var senderFriendRecord = new MemberRecord { FriendId = user.Id, OwnerId = senderId, Status = FriendRecordStatus.Accepted };

                await client.IndexAsync<MemberRecord>(senderFriendRecord, desc => desc.Routing(senderId));
                await Notify(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.Update, ItemId = senderId, Data = await CreateFriendDtoDetailed(targetFriendRecord) }, user.Id, cancellationToken);
                await Notify(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.Add, ItemId = user.Id, Data = await CreateFriendDtoDetailed(senderFriendRecord) }, senderId, cancellationToken);
            }
        }

        public async Task RemoveFriend(User user, string friendId, CancellationToken cancellationToken)
        {
            var client = await CreateClient<MemberRecord>();
            await client.DeleteAsync<MemberRecord>(user.Id + "_" + friendId, desc => desc.Routing(user.Id));
            await client.DeleteAsync<MemberRecord>(friendId + "_" + user.Id, desc => desc.Routing(friendId));
            //var r = await client.UpdateAsync<object>(friendId + "_" + user.Id, desc => desc.Doc(new { Status = FriendRecordStatus.RemovedByFriend }).FilterPath(new[] { "status" }).DocAsUpsert(false).Routing(friendId));

            await Notify(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.UpdateStatus, ItemId = user.Id, Data = new Friend { Status = FriendStatus.Disconnected } }, friendId, cancellationToken);
            await Notify(new FriendListUpdateDto { Operation = FriendListUpdateDtoOperation.Remove, ItemId = friendId, Data = new Friend { } }, user.Id, cancellationToken);
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

            var friends = await GetFriends(user.Id);

            var _ = Notify(new FriendListUpdateDto
            {
                ItemId = user.Id,
                Operation = FriendListUpdateDtoOperation.Update,
                Data = CreateFriendDtoDetailed(config, user, online)
            }, friends.Where(f => f.Status == FriendRecordStatus.Accepted).Select(f => f.FriendId), cancellationToken);
        }

        public async Task Subscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var sessions = scope.Resolve<IUserSessions>();
                var session = await sessions.GetSessionById(peer.SessionId, cancellationToken);

                if(session == null)
                {
                    throw new ClientException("NotAuthenticated");
                }

                var user = session.User;

                var statusConfig = await GetStatusConfig(user.Id);
                await _channel.AddPeer(user.Id, peer, statusConfig);
                var friendsRecords = await GetFriends(user.Id);
                var friends = new List<Friend>();
                foreach (var record in friendsRecords)
                {
                    friends.Add(await CreateFriendDtoDetailed(record));
                }
                var ctx = new GetFriendsCtx();
                ctx.Friends = friends;
                ctx.UserId = user.Id;
                await scope.ResolveAll<IFriendsEventHandler>().RunEventHandler(h => h.OnGetFriends(ctx), ex => { _logger.Log(LogLevel.Warn, "FriendsEventHandlers", "An error occured while executing the friends event handlers", ex); });
                foreach (var friend in friends)
                {
                    await Notify(new FriendListUpdateDto { ItemId = friend.UserId, Operation = FriendListUpdateDtoOperation.Add, Data = friend }, user.Id, cancellationToken);
                }
                var newStatus = ComputeStatus(statusConfig, true);
                if (newStatus == FriendStatus.Online)
                {
                    await Notify(new FriendListUpdateDto { ItemId = user.Id, Operation = FriendListUpdateDtoOperation.UpdateStatus, Data = new Friend { Status = newStatus } }, friendsRecords.Select(f => f.FriendId), cancellationToken);
                }
            }
        }

        public async Task Unsubscribe(IScenePeerClient peer,CancellationToken cancellationToken)
        {
            var config = await _channel.RemovePeer(peer.SessionId);
            if (config != null && config.Item1 != null)
            {
                var oldStatus = ComputeStatus(config.Item1, true);
                if (oldStatus != FriendStatus.Disconnected)
                {
                    var friends = await GetFriends(config.Item2);
                    await Notify(new FriendListUpdateDto { ItemId = config.Item2, Operation = FriendListUpdateDtoOperation.UpdateStatus, Data = new Friend { Status = FriendStatus.Disconnected } }, friends.Select(f => f.FriendId).ToArray(), cancellationToken);
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
            var records = await GetFriends(userId);

            return await Task.WhenAll(records.Select(r => CreateFriendDtoDetailed(r)));
        }

        private async Task<IEnumerable<MemberRecord>> GetFriends(string userId)
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
                _logger.Log(LogLevel.Error, "FriendsService.GetFriends", "an error occurred when trying to retrieve friends",
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
        public Task Notify(FriendListUpdateDto data, string userId, CancellationToken cancellationToken) => Notify(data, Enumerable.Repeat(userId, 1), cancellationToken);
        public async Task Notify(FriendListUpdateDto data, IEnumerable<string> userIds, CancellationToken cancellationToken)
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
            foreach (var friend in friends)
            {
                await Notify(new FriendListUpdateDto { ItemId = friend.UserId, Operation = FriendListUpdateDtoOperation.Add, Data = friend }, userId,cancellationToken);
            }
        }
    }
}
