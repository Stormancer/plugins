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


using Lucene.Net.Codecs.Lucene3x;
using Newtonsoft.Json.Linq;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;

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

    /// <summary>
    /// Configuration section for user handles.
    /// </summary>
    public class UserHandleConfigSection
    {
        internal const string PATH = "user.handle";

        /// <summary>
        /// Number of digits in the unique part of the pseudonym.
        /// </summary>
        public uint HashDigits { get; set; } = 4;

        /// <summary>
        /// Pattern used to validate user handles.
        /// </summary>
        public string Pattern { get; set; } = @"^[\p{Ll}\p{Lu}\p{Lt}\p{Lo}0-9-_.]*$";

        /// <summary>
        /// Max length of the handle (without the final hash)
        /// </summary>
        public uint MaxLength { get; set; } = 32;

        /// <summary>
        /// Appends an hash
        /// </summary>
        public bool AppendHash { get; set; } = true;
    }
    class UserService : IUserService
    {

        private readonly string _indexName;
        private readonly Func<IEnumerable<IUserIdentityIdProvider>> _userIdentityProviders;
        private readonly IUserStorage? _storage;
        private readonly ILogger _logger;
        private readonly Func<IEnumerable<IUserEventHandler>> _handlers;
        private readonly IConfiguration _configuration;
        private readonly IAnalyticsService _analytics;
        private readonly Random _random = new Random();

        private static bool _mappingChecked = false;
        private static AsyncLock _mappingCheckedLock = new AsyncLock();


        public UserService(
            Func<IEnumerable<IUserIdentityIdProvider>> userIdentityProviders,
            IUserStorage? storage,
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
            this._userIdentityProviders = userIdentityProviders;
            _storage = storage;
            _logger = logger;
            //_logger.Log(LogLevel.Trace, "users", $"Using index {_indexName}", new { index = _indexName });


        }


        public async Task<User> AddAuthentication(User user, string provider, string identifier, Action<dynamic> authDataModifier)
        {
            bool added = false;
            if (_storage != null)
            {
                (user, added) = await _storage.AddAuthentication(user, provider, identifier, authDataModifier);
            }
            else
            {
                var auth = user.Auth[provider];
                if (auth == null)
                {
                    auth = new JObject();
                    user.Auth[provider] = auth;
                }
                authDataModifier?.Invoke(auth);
            }


            var ctx = new AuthenticationChangedCtx(_storage == null ? AuthenticationChangedCtx.AuthenticationUpdateType.None : added ? AuthenticationChangedCtx.AuthenticationUpdateType.Add : AuthenticationChangedCtx.AuthenticationUpdateType.Update, provider, user);
            await _handlers().RunEventHandler(h => h.OnAuthenticationChanged(ctx), ex => _logger.Log(LogLevel.Error, "user.addAuth", "An error occurred while running OnAuthenticationChanged event handler", ex));

            return user;
        }

        public async Task<User> RemoveAuthentication(User user, string provider)
        {
            if (_storage != null)
            {
                user = await _storage.RemoveAuthentication(user, provider);
            }


            var ctx = new AuthenticationChangedCtx(_storage == null ? AuthenticationChangedCtx.AuthenticationUpdateType.None : AuthenticationChangedCtx.AuthenticationUpdateType.Remove, provider, user);
            await _handlers().RunEventHandler(h => h.OnAuthenticationChanged(ctx), ex => _logger.Log(LogLevel.Error, "user.removeAuth", "An error occured while running OnAuthenticationChanged event handler", ex));

            return user;
        }

        public async Task<User> CreateUser(string userId, JObject userData, string currentPlatform = "")
        {
            var user = new User() { Id = userId, LastPlatform = currentPlatform, UserData = userData };
            if (_storage != null)
            {
                user = await _storage.CreateUser(user);
            }


            _analytics.Push("user", "create", JObject.FromObject(new { UserId = user.Id, Platform = currentPlatform, createdOn = DateTime.UtcNow }));
            return user;
        }

        public async Task<string?> UpdateUserHandleAsync(string userId, string newHandle, CancellationToken cancellationToken)
        {
            var section = _configuration.GetValue(UserHandleConfigSection.PATH, new UserHandleConfigSection());
            string GenerateUserHandleWithHash(string handle)
            {
                var suffix = _random.Next(0, (int)Math.Pow(10, section.HashDigits));
                return handle + "#" + suffix;
            }

            // Check handle validity
            if (!Regex.IsMatch(newHandle, section.Pattern))
            {
                throw new ClientException("badHandle?badCharacters");
            }
            if (newHandle.Length > section.MaxLength)
            {
                throw new ClientException($"badHandle?tooLong&maxLength={section.MaxLength}");
            }

            if (_storage == null)
            {
                throw new NotSupportedException();
            }
            else
            {

                var ctx = new UpdateUserHandleCtx(userId, newHandle);
                await _handlers().RunEventHandler(handler => handler.OnUpdatingUserHandle(ctx), ex => _logger.Log(LogLevel.Error, "userSessions", "An exception was thrown by an OnUpdatingUserHandle event handler", ex));

                if (!ctx.Accept)
                {
                    throw new ClientException(ctx.ErrorMessage);
                }

                return await _storage.UpdateUserHandleAsync(userId, newHandle, section.AppendHash ? GenerateUserHandleWithHash : null, cancellationToken);
            }

        }

        public class UserLastLoginUpdate
        {
            public DateTime LastLogin { get; set; }
        }

        public class UserLastPlatformUpdate
        {
            public string LastPlatform { get; set; } = "";
        }



        public Task UpdateLastPlatform(string userId, string lastPlatform)
        {
            if (_storage != null)
            {
                return _storage.UpdateLastPlatform(userId, lastPlatform);
            }
            else
            {
                return Task.CompletedTask;
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


        private static Task<Dictionary<string, User?>> EmptyDictionaryTask = Task.FromResult(new Dictionary<string, User?>());

        public Task<Dictionary<string, User?>> GetUsersByIdentity(string provider, IEnumerable<string> identifiers)
        {

            if (_storage != null)
            {
                if (provider == Constants.PROVIDER_TYPE_STORMANCER)
                {
                    return _storage.GetUsers(identifiers, CancellationToken.None);
                }
                else
                {
                    return _storage.GetUsersByIdentity(provider, identifiers);
                }
            }
            else
            {
                return EmptyDictionaryTask;
            }

        }


        public async Task<Dictionary<PlatformId, User?>> GetUsersAsync(IEnumerable<PlatformId> platformIds)
        {
            var groups = platformIds.GroupBy(pId => pId.Platform);
            var r = new Dictionary<PlatformId, User?>();
            foreach (var group in groups)
            {
                var userIdentityProvider = GetUserIdentityProvider(group.Key);

                if (userIdentityProvider != null)
                {
                    var ids = new Dictionary<string, PlatformId>();
                    foreach (var pId in group)
                    {
                        
                        ids[await userIdentityProvider.GetIdentityInformation(pId)]= pId;
                    }
                    var results = await GetUsersByIdentity(group.Key, ids.Keys);

                    foreach (var (key, value) in results)
                    {
                        r[ids[key]] = value;
                    }
                }
                else
                {
                    var results = await GetUsersByIdentity(group.Key, group.Select(p => p.PlatformUserId));

                    foreach (var (key, value) in results)
                    {
                        r[new PlatformId(group.Key,key)] = value;
                    }
                }
            }
            return r;
        }

        private IUserIdentityIdProvider? GetUserIdentityProvider(string platform)
        {
            foreach (var userIdentityProvider in _userIdentityProviders())
            {
                if (userIdentityProvider.CanHandle(platform))
                {
                    return userIdentityProvider;
                }
            }
            return null;
        }
        public async Task<User?> GetUserByIdentity(PlatformId platformId)
        {
            var provider = platformId.Platform;
            var id = platformId.PlatformUserId;
            var userIdentityProvider = GetUserIdentityProvider(platformId.Platform);
            if (userIdentityProvider != null)
            {
                id = await userIdentityProvider.GetIdentityInformation(platformId);
            }

            return await GetUserByIdentity(provider, id);
        }

        public async Task<User?> GetUserByIdentity(string provider, string login)
        {
            if (provider == Constants.PROVIDER_TYPE_STORMANCER)
            {
                return await GetUser(login);
            }
            else
            {
                var users = await GetUsersByIdentity(provider, Enumerable.Repeat(login, 1));

                return users.TryGetValue(login, out var user) ? user : null;
            }
        }



        public Task<User?> GetUser(string uid)
        {
            if (_storage != null)
            {
                return _storage.GetUser(uid);
            }
            else
            {
                return Task.FromResult<User?>(null);
            }


        }

        public Task UpdateUserData<T>(string uid, T data)
        {
            if (_storage != null)
            {
                return _storage.UpdateUserData<T>(uid, data);
            }
            else
            {
                return Task.CompletedTask;
            }
        }
        public Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip)
        {
            if (_storage != null)
            {
                return _storage.QueryUserHandlePrefix(prefix, take, skip);
            }
            else
            {
                return Task.FromResult(Enumerable.Empty<User>());
            }

        }
        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            if (_storage != null)
            {
                return _storage.Query(query, take, skip, cancellationToken);
            }
            else
            {
                return Task.FromResult(Enumerable.Empty<User>());
            }
        }


        public Task Delete(string id)
        {
            if (_storage != null)
            {
                return _storage.Delete(id);
            }
            else
            {
                return Task.CompletedTask;
            }


        }

        public Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            if (_storage != null)
            {
                return _storage.GetUsers(userIds, cancellationToken);
            }
            else
            {
                return EmptyDictionaryTask;
            }

        }

        public Task<User?> GetUser(PlatformId platformId)
        {
            if (platformId.Platform == Constants.PROVIDER_TYPE_STORMANCER)
            {
                return GetUser(platformId.PlatformUserId);
            }
            else
            {
                return GetUserByIdentity(platformId.Platform, platformId.PlatformUserId);
            }
        }
    }
}
