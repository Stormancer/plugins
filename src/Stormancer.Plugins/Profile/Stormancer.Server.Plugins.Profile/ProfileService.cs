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
using Stormancer.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Stormancer.Server.Plugins.Users;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;

namespace Stormancer.Server.Plugins.Profile
{
    internal class ProfileService : IProfileService
    {
        private readonly IEnumerable<IProfilePartBuilder> _handlers;
        private readonly IEnumerable<ICustomProfilePart> customProfileParts;
        private readonly ILogger _logger;
        private readonly IUserService _users;

        public ProfileService(IEnumerable<IProfilePartBuilder> handlers,
            IEnumerable<ICustomProfilePart> customProfileParts,
            ILogger logger,
            IUserService users)
        {
            _handlers = handlers;
            this.customProfileParts = customProfileParts;
            _logger = logger;
            _users = users;
        }


        public async Task<Dictionary<string, Dictionary<string, JObject>?>> GetProfiles(IEnumerable<string> userIds, Dictionary<string, string> displayOptions, Session? requestingUser, CancellationToken cancellationToken)
        {
            var dic = new ConcurrentDictionary<string, ConcurrentDictionary<string, JObject>>();
           
            var ctx = new ProfileCtx(userIds, dic, displayOptions, requestingUser);

            await _handlers.RunEventHandler(h => h.GetProfiles(ctx, cancellationToken), ex => _logger.Log(LogLevel.Error, "profile", "An error occurred while getting profiles.", ex));

            var result = new Dictionary<string, Dictionary<string, JObject>?>();
            foreach(var id in userIds)
            {
                if(dic.TryGetValue(id,out var data))
                {
                    result[id] = data.ToDictionary(d => d.Key, d => d.Value);
                }
                else
                {
                    result[id] = null;
                }
            }
            return result;
        }

        public Task DeleteCustomProfilePart(string userId, string partId, bool fromClient)
        {
            return customProfileParts.RunEventHandler(p => p.DeleteAsync(userId, partId, fromClient), ex => _logger.Log(LogLevel.Error, "profile", "An error occurred while deleting a custom part.", ex));
        }

        public async Task UpdateCustomProfilePart(string userId, string partId, string version, bool fromClient, Stream inputStream)
        {
            var ctx = new UpdateCustomProfilePartContext(userId, partId, version, fromClient, inputStream);
            await  customProfileParts.RunEventHandler(p => p.UpdateAsync(ctx), ex => _logger.Log(LogLevel.Error, "profile", "An error occured while updating a custom profile part.", ex));

            if(ctx.Processed == false)
            {
                _logger.Log(LogLevel.Warn, "profiles.customParts", $"A custom profile part update was received, but not processed : {partId}", new { userId, partId, version, fromClient });
            }
        }

        public Task<string?> UpdateUserHandle(string userId, string newHandle, CancellationToken cancellationToken)
        {
            return _users.UpdateUserHandleAsync(userId, newHandle, cancellationToken);
        }
    }
}
