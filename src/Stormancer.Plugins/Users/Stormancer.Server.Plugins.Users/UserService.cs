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

using Microsoft.EntityFrameworkCore;
using Nest;
using Newtonsoft.Json.Linq;
using Server.Plugins.Users;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LogLevel = Stormancer.Diagnostics.LogLevel;

namespace Stormancer.Server.Plugins.Users
{
    class ConflictException : Exception
    {
    }

    class UserService : IUserService
    {

        private readonly string _indexName;
        private readonly DbContextAccessor _dbContext;
        private readonly ILogger _logger;
        private readonly Func<IEnumerable<IUserEventHandler>> _handlers;
        private readonly Random _random = new Random();

        private static bool _mappingChecked = false;
        private static AsyncLock _mappingCheckedLock = new AsyncLock();


        public UserService(
            DbContextAccessor dbContext,
            IEnvironment environment,
            ILogger logger,
            Func<IEnumerable<IUserEventHandler>> eventHandlers
        )
        {
            _indexName = (string?)(environment.Configuration.users?.index) ?? "gameData";
            _handlers = eventHandlers;
            _dbContext = dbContext;
            _logger = logger;
            //_logger.Log(LogLevel.Trace, "users", $"Using index {_indexName}", new { index = _indexName });


        }


        public async Task<User> AddAuthentication(User user, string provider, string identifier, Action<dynamic> authDataModifier)
        {
            var c = await _dbContext.GetDbContextAsync();

            var auth = user.Auth[provider];
            if (auth == null)
            {
                auth = new JObject();
                user.Auth[provider] = auth;
            }
            authDataModifier?.Invoke(auth);

            var userId = Guid.Parse(user.Id);
            var record = c.Set<UserRecord>().Include(u => u.Identities.Where(i => i.Provider == provider && i.Identity == identifier)).SingleOrDefault(u => u.Id == userId);

            if (!record.Identities.Any())
            {
                await c.Set<IdentityRecord>().AddAsync(new IdentityRecord { Provider = provider, Identity = identifier, User = record });
            }
            record.Auth = auth.ToString();


            await c.SaveChangesAsync();

            var ctx = new AuthenticationChangedCtx { Type = provider, User = user };
            await _handlers().RunEventHandler(h => h.OnAuthenticationChanged(ctx), ex => _logger.Log(LogLevel.Error, "user.addAuth", "An error occured while running OnAuthenticationChanged event handler", ex));

            return user;
        }

        public async Task<User> RemoveAuthentication(User user, string provider)
        {
            var c = await _dbContext.GetDbContextAsync();

            user.Auth.Remove(provider);
            var userId = Guid.Parse(user.Id);
            var record = c.Set<UserRecord>().Include(u => u.Identities.Where(i => i.Provider == provider)).SingleOrDefault(u => u.Id == userId);

            if (record.Identities.Any())
            {
                c.Set<IdentityRecord>().RemoveRange(record.Identities);
            }

            record.Auth = user.Auth.ToString();

            await c.SaveChangesAsync();


            await _handlers().RunEventHandler(h => h.OnAuthenticationChanged(ctx), ex => _logger.Log(LogLevel.Error, "user.removeAuth", "An error occured while running OnAuthenticationChanged event handler", ex));

            return user;
        }

        public async Task<User> CreateUser(string userId, JObject userData, string currentPlatform = "")
        {
            var user = new User() { Id = userId, LastPlatform = currentPlatform, UserData = userData, };
            var record = new UserRecord()
            {
                Auth = "{}",
                Channels = "{}",
                CreatedOn = DateTime.UtcNow,
                Id = Guid.Parse(userId),
                Pseudonym = 
            };
            var esClient = await Client<User>();
            await esClient.IndexAsync(user, s => s.Refresh(Elasticsearch.Net.Refresh.WaitFor));
            return user;
        }

        public Task<string> UpdateUserHandle(string userId, string newHandle, bool appendHash, CancellationToken cancellationToken)
        {

        }

        public class UserLastLoginUpdate
        {
            public DateTime LastLogin { get; set; }
        }

        public class UserLastPlatformUpdate
        {
            public string LastPlatform { get; set; } = "";
        }

        public async Task UpdateLastLoginDate(string userId)
        {
            var c = await Client<User>();
            await c.UpdateAsync<User, UserLastLoginUpdate>(userId,
                u => u.Doc(new UserLastLoginUpdate { LastLogin = DateTime.UtcNow })
            );
        }

        public async Task UpdateLastPlatform(string userId, string lastPlatform)
        {
            var c = await Client<User>();
            await c.UpdateAsync<User, UserLastPlatformUpdate>(userId,
                u => u.Doc(new UserLastPlatformUpdate { LastPlatform = lastPlatform })
            );
        }

        private class ClaimUser
        {
            public ClaimUser(string login, string cacheId)
            {
                Login = login;
                CacheId = cacheId;
            }
            public string Login { get; set; }
            public string CacheId { get; set; }
            public AuthenticationClaim? Claim { get; set; } = null;
            public User? User { get; set; } = null;
        }




        public async Task<Dictionary<string, User?>> GetUsersByClaim(string provider, string claimPath, string[] logins)
        {
            var c = await Client<User>();

            // Create datas
            var datas = logins.Select(login => new ClaimUser(login, GetCacheId(provider, claimPath, login))).ToArray();

            // Get all auth claims
            var response = await c.MultiGetAsync(desc => desc.GetMany<AuthenticationClaim>(datas.Select(data => data.CacheId), (desc, _) => desc.Index(GetIndex<AuthenticationClaim>())));

            // Get users for found claims
            var usersToGet = new List<string>();
            var searchUsersTasks = new List<Task>();
            foreach (var data in datas)
            {
                // Set claim
                data.Claim = response.Source<AuthenticationClaim>(data.CacheId);
                if (data.Claim != null)
                {
                    // Populate users for found claims
                    usersToGet.Add(data.Claim.UserId);
                }
                else
                {
                    // Search user if no claim
                    async Task SearchUser()
                    {
                        var r = await c.SearchAsync<User>(sd => sd.Query(qd => qd.Term($"auth.{provider}.{claimPath}", data.Login)));

                        if (r.Hits.Count() > 1)
                        {
                            data.User = await MergeUsers(r.Hits.Select(h => h.Source));
                        }
                        else
                        {
                            var h = r.Hits.FirstOrDefault();

                            if (h != null)
                            {

                                data.User = h.Source;
                            }
                            else
                            {
                                return;
                            }
                        }

                        // Create cached claim
                        await c.IndexAsync(new AuthenticationClaim { Id = data.CacheId, UserId = data.User.Id }, s => s.Index(GetIndex<AuthenticationClaim>()));
                    }
                    searchUsersTasks.Add(SearchUser());
                }
            }

            // Get users from db
            var response2 = await c.MultiGetAsync(desc => desc.GetMany<User>(usersToGet));

            await Task.WhenAll(searchUsersTasks);

            // Get users by cached claims
            var users = response2.GetMany<User>(usersToGet).Where(hit => hit.Found).Select(hit => hit.Source).ToArray();

            foreach (var user in response2.GetMany<User>(usersToGet).Where(hit => !hit.Found))
            {
                var data = datas.First(data2 => data2.Claim?.UserId == user.Id);
                if (data.Claim != null)
                {
                    await c.DeleteAsync<AuthenticationClaim>(data.Claim.Id, s => s.Index(GetIndex<AuthenticationClaim>()));
                }
            }
            // Populate users from cached claims
            foreach (var user in users)
            {
                var data = datas.First(data2 => data2.Claim?.UserId == user.Id);
                data.User = user;
            }

            // Return found users
            return datas.ToDictionary(data => data.Login, data => data.User);
        }

        public async Task<User?> GetUserByClaim(string provider, string claimPath, string login)
        {
            var c = await Client<User>();
            var cacheId = GetCacheId(provider, claimPath, login);
            var claim = await c.GetAsync<AuthenticationClaim>(cacheId, s => s.Index(GetIndex<AuthenticationClaim>()));
            if (claim.Found)
            {
                var r = await c.GetAsync<User>(claim.Source.UserId);
                if (r.Found)
                {
                    return r.Source;
                }
                else
                {
                    await c.DeleteAsync<AuthenticationClaim>(cacheId, s => s.Index(GetIndex<AuthenticationClaim>()));
                    return null;
                }
            }
            else if (claim.IsValid || claim.ServerError == null || claim.ServerError.Status == 404)
            {
                var r = await c.SearchAsync<User>(sd => sd.Query(qd => qd.Term($"auth.{provider}.{claimPath}", login)).AllowNoIndices());

                User user;
                if (r.Hits.Count() > 1)
                {
                    user = await MergeUsers(r.Hits.Select(h => h.Source));
                }
                else
                {
                    var h = r.Hits.FirstOrDefault();

                    if (h != null)
                    {

                        user = h.Source;
                    }
                    else
                    {
                        return null;
                    }
                }
                await c.IndexAsync(new AuthenticationClaim { Id = cacheId, UserId = user.Id }, s => s.Index(GetIndex<AuthenticationClaim>()));

                return user;
            }
            else
            {
                _logger.Log(LogLevel.Error, "users", "Get user by claim failed.", new { error = claim.ServerError });
                throw new InvalidOperationException("Failed to get user.");
            }
        }

        private async Task<User> MergeUsers(IEnumerable<User> users)
        {
            var handlers = _handlers();
            foreach (var handler in handlers)
            {
                await handler.OnMergingUsers(users);
            }

            var sortedUsers = users.OrderBy(u => u.CreatedOn).ToList();
            var mainUser = sortedUsers.First();

            var data = new Dictionary<IUserEventHandler, object>();
            foreach (var handler in handlers)
            {
                data[handler] = await handler.OnMergedUsers(sortedUsers.Skip(1), mainUser);
            }

            var c = await Client<User>();
            _logger.Log(Stormancer.Diagnostics.LogLevel.Info, "users", "Merging users.", new { deleting = sortedUsers.Skip(1), into = mainUser });
            await c.BulkAsync(desc =>
            {
                desc = desc.DeleteMany<User>(sortedUsers.Skip(1).Select(u => u.Id))
                           .Index<User>(i => i.Document(mainUser));
                foreach (var handler in handlers)
                {
                    desc = handler.OnBuildMergeQuery(sortedUsers.Skip(1), mainUser, data[handler], desc);
                }
                return desc;
            });
            return mainUser;
        }

        public async Task<User?> GetUser(string uid)
        {
            var c = await Client<User>();
            var r = await c.GetAsync<User>(uid);
            if (r.Source != null)
            {
                return r.Source;
            }
            else
            {
                return default;
            }
        }

        public async Task UpdateUserData<T>(string uid, T data)
        {
            var user = await GetUser(uid);
            if (user == null)
            {
                throw new InvalidOperationException($"Update failed: User {uid} not found.");
            }
            else
            {
                user.UserData = JObject.FromObject(data!);
                await (await Client<User>()).IndexAsync(user, s => s);
            }
        }
        public async Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip)
        {
            var c = await Client<User>();
            var result = await c.SearchAsync<User>(s =>
                s.Query(
                    q => q.MatchPhrasePrefix(desc => desc.Field("userData.handle").Query(prefix))
                    )
            );

            return result.Documents;
        }
        public async Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            var c = await Client<User>();

            // TODO: need this to handle dashes properly
            //var result = await c.SearchAsync<User>(s => s.Query(q => q.QueryString(qs => qs.Query(query).DefaultField("userData.handle") )).Size(take).Skip(skip));
            var mustClauses = query.Select<KeyValuePair<string, string>, Func<QueryContainerDescriptor<User>, QueryContainer>>(i =>
            {
                return cd => cd.Match(m => m.Field(i.Key).Query(i.Value));
            }).ToArray();
            var result = await c.SearchAsync<User>(s =>
            {
                if (mustClauses.Any())
                {
                    return s.Query(
                    q => q.Bool(b => b.Must(mustClauses))
                    );
                }
                else
                {
                    return s;
                }

            }, cancellationToken);

            return result.Documents;
        }

        public async Task UpdateCommunicationChannel(string userId, string channel, JObject data)
        {
            var user = await GetUser(userId);

            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            else
            {
                user.Channels[channel] = JObject.FromObject(data);
                await (await Client<User>()).IndexAsync(user, s => s);
            }

        }

        public async Task Delete(string id)
        {
            var user = await GetUser(id);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var c = await Client<User>();

            var response = await c.DeleteAsync<User>(user.Id);

            if (!response.IsValid)
            {
                throw new InvalidOperationException("DB error : " + response.ServerError.ToString());
            }
        }

        public async Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            var c = await Client<User>();
            var r = await c.MultiGetAsync(s => s.GetMany<User>(userIds.Distinct()), cancellationToken);
            var sources = r.GetMany<User>(userIds);
            return sources.ToDictionary(s => s.Id, s => s.Found ? s.Source : default);
        }


    }
}
