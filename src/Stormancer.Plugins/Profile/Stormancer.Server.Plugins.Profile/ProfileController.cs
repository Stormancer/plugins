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
using System.Threading;

namespace Stormancer.Server.Plugins.Profile
{
   
    [Service]
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
        public async Task<Dictionary<string, ProfileDto?>> GetProfilesBySessionIds(IEnumerable<string> sessionIds, Dictionary<string, string> displayOptions, RequestContext<IScenePeerClient> ctx)
        {

            var sessions = await _sessions.GetSessions(sessionIds, ctx.CancellationToken);

            IEnumerable<string> userIds = sessions.Values.Select(s => s?.User?.Id).Where(id => id != null)!;

            var profiles = userIds.Any() ? await GetProfiles(userIds, displayOptions, ctx.CancellationToken) : new Dictionary<string, ProfileDto>();


            var results = new Dictionary<string, ProfileDto?>();

            foreach (var session in sessions)
            {
                var userId = session.Value?.User?.Id;
                if (userId != null && profiles.TryGetValue(userId, out var profile))
                {
                    results.Add(session.Key, profile);
                }
            }
            return results;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<string, ProfileDto>> GetProfiles(IEnumerable<string> userIds, Dictionary<string, string> displayOptions, RequestContext<IScenePeerClient> ctx)
        {

            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }

            var profiles = await _profiles.GetProfiles(userIds, displayOptions, session, ctx.CancellationToken);
            return profiles.ToDictionary(kvp => kvp.Key, kvp => new ProfileDto { Data = kvp.Value.ToDictionary(kvp2 => kvp2.Key, kvp2 => kvp2.Value.ToString()) });
        }

        [S2SApi]
        public async Task<Dictionary<string, ProfileDto>> GetProfiles(IEnumerable<string> userIds, Dictionary<string, string> displayOptions, CancellationToken cancellationToken)
        {
            var profiles = await _profiles.GetProfiles(userIds, displayOptions, null, cancellationToken);
            return profiles.ToDictionary(kvp => kvp.Key, kvp => new ProfileDto { Data = kvp.Value.ToDictionary(kvp2 => kvp2.Key, kvp2 => kvp2.Value.ToString()) });
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> UpdateUserHandle(string handle, RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
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
                return await _profiles.UpdateUserHandle(user.Id, handle, ctx.CancellationToken);
            }
            catch (RpcException ex)
            {
                throw new ClientException(ex);
            }
        }


        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<string, ProfileDto>> QueryProfiles(string pseudoPrefix, int skip, int take, RequestContext<IScenePeerClient> ctx)
        {
            if (pseudoPrefix.Length < 3)
            {
                throw new ClientException("profiles.query.notEnoughCharacters?minLength=3");
            }
            var users = await _users.QueryUserHandlePrefix(pseudoPrefix, take, skip);
            var profiles = await _profiles.GetProfiles(users.Select(u => u.Id), new Dictionary<string, string> { { "displayType", "summary" } }, await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken), ctx.CancellationToken);
            return profiles.ToDictionary(kvp => kvp.Key, kvp => new ProfileDto { Data = kvp.Value.ToDictionary(kvp2 => kvp2.Key, kvp2 => kvp2.Value.ToString()) });
        }

        /// <summary>
        /// Updates a profile part
        /// </summary>
        /// <param name="partId"></param>
        /// <param name="partFormatVersion">Version of the part format.</param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task UpdateCustomProfilePart(string partId, string partFormatVersion, RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);

            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }
            var user = session.User;
            if (user == null)
            {
                throw new ClientException("anonymousUser");
            }

            await _profiles.UpdateCustomProfilePart(user.Id, partId, partFormatVersion, true, ctx.InputStream);
        }

        /// <summary>
        /// Deletes a profile part.
        /// </summary>
        /// <param name="partId"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task DeleteCustomProfilePart(string partId, RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);

            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }
            var user = session.User;
            if (user == null)
            {
                throw new ClientException("anonymousUser");
            }

            await _profiles.DeleteCustomProfilePart(user.Id, partId, true);
        }
    }

    /// <summary>
    /// An user profile object sent to clients.
    /// </summary>
    public class ProfileDto
    {
        /// <summary>
        /// Gets the dictionary of parts in the profile, represented as json.
        /// </summary>
        [MessagePackMember(0)]
        public Dictionary<string, string> Data { get; set; } = default!;
    }
}
