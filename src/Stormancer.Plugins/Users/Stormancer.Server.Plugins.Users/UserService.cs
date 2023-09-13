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
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LogLevel = Stormancer.Diagnostics.LogLevel;

namespace Stormancer.Server.Plugins.Users
{
    class ConflictException : Exception
    {
    }

    public class UserHandleConfigSection
    {
        internal const string PATH = "user.handle";

        /// <summary>
        /// Number of digits in the unique part of the pseudonym.
        /// </summary>
        public uint HashDigits { get; set; } = 4;

        public string Pattern { get; set; } = @"^[\p{Ll}\p{Lu}\p{Lt}\p{Lo}0-9-_.]*$";

        public uint MaxLength { get; set; } = 32;

        public bool AppendHash { get; set; } = true;
    }
    class UserService : IUserService
    {

        private readonly string _indexName;
        private readonly DbContextAccessor _dbContext;
        private readonly ILogger _logger;
        private readonly Func<IEnumerable<IUserEventHandler>> _handlers;
        private readonly IConfiguration _configuration;
        private readonly IAnalyticsService _analytics;
        private readonly Random _random = new Random();

        private static bool _mappingChecked = false;
        private static AsyncLock _mappingCheckedLock = new AsyncLock();


        public UserService(
            DbContextAccessor dbContext,
            IEnvironment environment,
            ILogger logger,
            Func<IEnumerable<IUserEventHandler>> eventHandlers,
            IConfiguration configuration,
            IAnalyticsService analytics
        )
        {
            _indexName = (string?)(environment.Configuration.users?.index) ?? "gameData";
            _handlers = eventHandlers;
            _configuration = configuration;
            _analytics = analytics;
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


            var identity = record.Identities.FirstOrDefault();
            var added = identity == null;
            if (identity == null)
            {
                identity = new IdentityRecord { Provider = provider, Identity = identifier };
                identity.Users = new List<UserRecord>
                {
                    record
                };

                await c.Set<IdentityRecord>().AddAsync(identity);
            }
            identity.MainUser = record;
            record.Auth = JsonDocument.Parse(user.Auth.ToString());


            await c.SaveChangesAsync();

            var ctx = new AuthenticationChangedCtx(added ? AuthenticationChangedCtx.AuthenticationUpdateType.Add : AuthenticationChangedCtx.AuthenticationUpdateType.Update, provider, user);
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

            record.Auth = JsonDocument.Parse(user.Auth.ToString());

            await c.SaveChangesAsync();

            var ctx = new AuthenticationChangedCtx(AuthenticationChangedCtx.AuthenticationUpdateType.Remove, provider, user);
            await _handlers().RunEventHandler(h => h.OnAuthenticationChanged(ctx), ex => _logger.Log(LogLevel.Error, "user.removeAuth", "An error occured while running OnAuthenticationChanged event handler", ex));

            return user;
        }

        public async Task<User> CreateUser(string userId, JObject userData, string currentPlatform = "")
        {
            var user = new User() { Id = userId, LastPlatform = currentPlatform, UserData = userData, LastLogin = string.IsNullOrEmpty(currentPlatform) ? new DateTime() : DateTime.UtcNow };
            var record = UserRecord.CreateRecordFromUser(user);

            var dbContext = await this._dbContext.GetDbContextAsync();
            await dbContext.Set<UserRecord>().AddAsync(record);

            await dbContext.SaveChangesAsync();
            _analytics.Push("user", "create", JObject.FromObject(new { UserId=user.Id, Platform = currentPlatform, createdOn = DateTime.UtcNow }));
            return user;
        }

        public async Task<string?> UpdateUserHandleAsync(string userId, string newHandle, CancellationToken cancellationToken)
        {
            string GenerateUserHandleWithHash(string handle, UserHandleConfigSection section)
            {
                var suffix = _random.Next(0, (int)Math.Pow(10, section.HashDigits));
                return handle + "#" + suffix;
            }



            var section = _configuration.GetValue(UserHandleConfigSection.PATH, new UserHandleConfigSection());


            // Check handle validity
            if (!Regex.IsMatch(newHandle, section.Pattern))
            {
                throw new ClientException("badHandle?badCharacters");
            }
            if (newHandle.Length > section.MaxLength)
            {
                throw new ClientException($"badHandle?tooLong&maxLength={section.MaxLength}");
            }

            var ctx = new UpdateUserHandleCtx(userId, newHandle);
            await _handlers().RunEventHandler(handler => handler.OnUpdatingUserHandle(ctx), ex => _logger.Log(LogLevel.Error, "usersessions", "An exception was thrown by an OnUpdatingUserHandle event handler", ex));

            if (!ctx.Accept)
            {
                throw new ClientException(ctx.ErrorMessage);
            }
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(userId);
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);

            var userData = JObject.Parse(record.UserData.RootElement.GetRawText()!);
            if (userData.TryGetValue(EphemeralAuthenticationProvider.IsEphemeralKey, out var isEphemeral) && (bool)isEphemeral)
            {
                throw new NotSupportedException();
                //await UpdateHandleEphemeral();
            }

            if (section.AppendHash)
            {
                int tries = 0;
                bool success = false;
                do
                {
                    try
                    {
                        record.UserHandle = GenerateUserHandleWithHash(newHandle, section);
                        await dbContext.SaveChangesAsync();
                        success = true;
                    }
                    catch (Exception)
                    {
                        if (tries > 5)
                        {
                            throw;
                        }
                        tries++;

                    }
                }
                while (!success);
            }
            else
            {
                record.UserHandle = newHandle;
                await dbContext.SaveChangesAsync();
            }


            return record.UserHandle;
        }

        public class UserLastLoginUpdate
        {
            public DateTime LastLogin { get; set; }
        }

        public class UserLastPlatformUpdate
        {
            public string LastPlatform { get; set; } = "";
        }



        public async Task UpdateLastPlatform(string userId, string lastPlatform)
        {
            var guid = Guid.Parse(userId);
            var dbContext = await _dbContext.GetDbContextAsync();
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);
            if (record != null)
            {
                record.LastLogin = DateTime.Now;
                record.LastPlatform = lastPlatform;
                await dbContext.SaveChangesAsync();
            }
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




        public async Task<Dictionary<string, User?>> GetUsersByIdentity(string provider, IEnumerable<string> identifiers)
        {
            var c = await _dbContext.GetDbContextAsync();

            var ids = identifiers.ToList();

            var records = await c.Set<UserRecord>().Where(u => u.Identities.Any(i => i.Provider == provider && ids.Contains(i.Identity))).Include(u => u.Identities).ToListAsync();

            var result = new Dictionary<string, User?>();
            foreach (var id in ids)
            {
                var record = records.FirstOrDefault(r => r.Identities.Any(r => r.Identity == id && r.Provider == provider));
                result[id] = UserRecord.CreateUserFromRecord(record);
            }

            return result;
        }

        public async Task<User?> GetUserByIdentity(string provider, string login)
        {
            var users = await GetUsersByIdentity(provider, Enumerable.Repeat(login, 1));

            return users[login];
        }



        public async Task<User?> GetUser(string uid)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(uid);
            return UserRecord.CreateUserFromRecord(await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid));

        }

        public async Task UpdateUserData<T>(string uid, T data)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(uid);
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);
            if (record == null)
            {
                throw new InvalidOperationException($"notfound");
            }
            else
            {
                if (data is JObject jObject)
                {
                    record.UserData = JsonDocument.Parse(jObject.ToString());
                }
                else
                {
                    record.UserData = JsonSerializer.SerializeToDocument(data!);
                }

                
                await dbContext.SaveChangesAsync();
            }
        }
        public async Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var records = await dbContext.Set<UserRecord>().Where(u => u.UserHandle != null && u.UserHandle.StartsWith(prefix)).Skip(skip).Take(take).ToListAsync();

            return records.Select(r => UserRecord.CreateUserFromRecord(r));

        }
        public async Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var mainQuery = dbContext.Set<UserRecord>().AsQueryable().AsNoTracking();


            foreach (var (key, value) in query)
            {
                mainQuery = mainQuery.Where(r => r.UserData.RootElement.GetProperty("{" + key.Replace('.', ',') + "}").ToString() == value);
            }

            var results = await mainQuery.ToListAsync();

            return results.Select(r => UserRecord.CreateUserFromRecord(r));
        }

        public async Task UpdateCommunicationChannel(string userId, string channel, JsonObject data)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(userId);
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);
            if (record == null)
            {
                throw new InvalidOperationException($"notfound");
            }
            else
            {
                var obj = JsonObject.Create(record.Channels.RootElement);
                obj![channel] = data;

                record.Channels = obj.Deserialize<JsonDocument>()!;
                await dbContext.SaveChangesAsync();
            }

        }

        public async Task Delete(string id)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(id);

            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);

            if (record != null)
            {
                dbContext.Set<UserRecord>().Remove(record);
                await dbContext.SaveChangesAsync();
            }

        }

        public async Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            
            var dbContext = await _dbContext.GetDbContextAsync();


            try
            {
                var guids = userIds.Select(Guid.Parse).ToArray();
                var records = await dbContext.Set<UserRecord>().Where(u => guids.Contains(u.Id)).ToListAsync();
                var results = new Dictionary<string, User?>();
                foreach (var id in userIds)
                {
                    results[id] = UserRecord.CreateUserFromRecord(records.FirstOrDefault(r => r.Id == Guid.Parse(id)));
                }
                return results;
            }
            catch(FormatException ex)
            {
                _logger.Log(LogLevel.Error,"users", $"Failed to parse userIds ({string.Join(",", userIds)})", ex);
                throw;
            }
        }


    }
}
