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

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    [ApiController]
    [Route("_users")]
    public class UsersAdminController : ControllerBase
    {
        private readonly IUserService _users;
        private readonly IAuthenticationService auth;
        private readonly ISceneHost scene;


        public UsersAdminController(IUserService users, IAuthenticationService auth, ISceneHost scene)
        {
            _users = users;
            this.auth = auth;
            this.scene = scene;

        }

        [HttpGet]
        [Route("getByClaim")]
        public async Task<User> GetByClaim(string provider, string claimPath, string claimValue)
        {
            return await _users.GetUserByClaim(provider, claimPath, claimValue);
        }

        [HttpGet]
        [Route("search")]
        public async Task<IEnumerable<UserViewModel>> Search(int take = 20, int skip = 0)
        {
            var query = Request.Query.ToDictionary(s => s.Key, s => s.Value.FirstOrDefault());
            var users = await _users.Query(query, take, skip);

            return users.Select(user => new UserViewModel { id = user.Id });
        }

        /// <summary>
        /// Gets an user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{id}")]
        public Task<User> Get(string id)
        {
            return _users.GetUser(id);
        }

        /// <summary>
        /// Deletes an user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("{id}")]
        public Task Delete(string id)
        {
            return _users.Delete(id);
        }

        /// <summary>
        /// Unlink an authentication provider from an user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("{userId}/_auth/{provider}")]
        public async Task Unlink(string userId, string provider)
        {
            var user = await _users.GetUser(userId);
            await auth.Unlink(user, provider);
        }

        /// <summary>
        /// kicks a player from the server.
        /// </summary>
        /// <param name="id">Id of the user to kick.</param>
        /// <param name="args">Kick options.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("{userId}/_kick")]
        public Task Kick(string id, [FromBody] KickArguments args)
        {
            using var scope = scene.CreateRequestScope();
            return scope.Resolve<IUserSessions>().KickUser(id, args.reason ?? "adminRequest");
        }


    }

    /// <summary>
    /// Arguments passed to the kick API.
    /// </summary>
    public class KickArguments
    {
        /// <summary>
        /// Reason the player was kick out of the server.
        /// </summary>
        /// <remarks>
        /// *the reason is sent to the client.
        /// </remarks>
        public string? reason { get; set; }
    }


    public class UserViewModel
    {
        public string id { get; set; }

    }
}
