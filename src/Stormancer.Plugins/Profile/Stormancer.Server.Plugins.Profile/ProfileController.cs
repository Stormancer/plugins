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
using MsgPack.Serialization;
using Stormancer.Server.Plugins.API;
using Stormancer.Core;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.Profile
{
    class ProfileController : ControllerBase
    {
        private readonly IUserSessions _sessions;
        private readonly IProfileService _profiles;
        private readonly ISerializer _serializer;
        private readonly IUserService _users;

        public ProfileController(IProfileService profiles, ISerializer serializer, IUserSessions sessions, IUserService users)
        {
            _sessions = sessions;
            _profiles = profiles;
            _serializer = serializer;
            _users = users;
        }
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<string, ProfileDto>> GetProfiles(IEnumerable<string> userIds, Dictionary<string, string> displayOptions)
        {

            var session = await _sessions.GetSession(Request.RemotePeer);
            var profiles = await _profiles.GetProfiles(userIds, displayOptions, session);
            return profiles.ToDictionary(kvp => kvp.Key, kvp => new ProfileDto { Data = kvp.Value.ToDictionary(kvp2 => kvp2.Key, kvp2 => kvp2.Value.ToString()) });
        }

        public async Task GetProfileInternal(RequestContext<IScenePeer> ctx)
        {
            var userIds = _serializer.Deserialize<IEnumerable<string>>(ctx.InputStream);
            var displayOptions = _serializer.Deserialize<Dictionary<string, string>>(ctx.InputStream);
            var profiles = await _profiles.GetProfiles(userIds, displayOptions, null);
            await ctx.SendValue(s => _serializer.Serialize(profiles, s));
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> UpdateUserHandle(string handle)
        {
            var session = await _sessions.GetSession(Request.RemotePeer);
            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }
            var user = session.User;
            if (user == null)
            {
                throw new ClientException("anonymousUser");
            }
            try
            {
                return await _profiles.UpdateUserHandle(user.Id, handle);
            }
            catch (RpcException ex)
            {
                throw new ClientException(ex);
            }
        }


        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<string, ProfileDto>> QueryProfiles(string pseudoPrefix, int skip, int take)
        {
            if (pseudoPrefix.Length < 3)
            {
                throw new ClientException("profiles.query.notEnoughCharacters?minLength=3");
            }
            var users = await _users.QueryUserHandlePrefix(pseudoPrefix, take, skip);
            var profiles = await _profiles.GetProfiles(users.Select(u => u.Id), new Dictionary<string, string> { { "displayType", "summary" } }, await _sessions.GetSession(this.Request.RemotePeer));
            return profiles.ToDictionary(kvp => kvp.Key, kvp => new ProfileDto { Data = kvp.Value.ToDictionary(kvp2 => kvp2.Key, kvp2 => kvp2.Value.ToString()) });
        }

        /// <summary>
        /// Updates a profile part
        /// </summary>
        /// <param name="partId"></param>
        /// <param name="partFormatVersion">Version of the part format.</param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task UpdateCustomProfilePart(string partId, string partFormatVersion)
        {
            var session = await _sessions.GetSession(Request.RemotePeer);

            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }
            var user = session.User;
            if (user == null)
            {
                throw new ClientException("anonymousUser");
            }

            await _profiles.UpdateCustomProfilePart(user.Id, partId, partFormatVersion, true,Request.InputStream);
        }

        /// <summary>
        /// Deletes a profile part.
        /// </summary>
        /// <param name="partId"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task DeleteCustomProfilePart(string partId)
        {
            var session = await _sessions.GetSession(Request.RemotePeer);

            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }
            var user = session.User;
            if (user == null)
            {
                throw new ClientException("anonymousUser");
            }

            await _profiles.DeleteCustomProfilePart(user.Id, partId,true);
        }
    }


    public class ProfileDto
    {
        [MessagePackMember(0)]
        public Dictionary<string, string> Data { get; set; }
    }
}
