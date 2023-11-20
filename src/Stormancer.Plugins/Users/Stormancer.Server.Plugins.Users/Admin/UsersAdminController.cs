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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides administrative actions on users.
    /// </summary>
    [ApiController]
    [Route("_users")]
    public class UsersAdminController : ControllerBase
    {
        
        private readonly ISceneHost scene;
        private readonly IUserService _users;

        /// <summary>
        /// Controller constructor.
        /// </summary>
        /// <param name="users"></param>
        /// <param name="scene"></param>
        public UsersAdminController(ISceneHost scene, IUserService users)
        {
            this.scene = scene;
            _users = users;
        }


        /// <summary>
        /// Gets an user using a claim.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="identifier"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getByClaim")]
        public async Task<User?> GetByClaim(string provider, string identifier, CancellationToken cancellationToken)
        {
            await using var scope = scene.CreateRequestScope();
            var users = scope.Resolve<IUserService>();
            return await users.GetUserByIdentity(provider, identifier);
        }

        /// <summary>
        /// Search for users using the query parameters.
        /// </summary>
        /// <param name="take"></param>
        /// <param name="skip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("")]
        public async Task<IEnumerable<UserViewModel>> Search(int take = 20, int skip = 0, CancellationToken cancellationToken = default)
        {
            await using var scope = scene.CreateRequestScope();
            var _users = scope.Resolve<IUserService>();
            var query = Request.Query.Where(s => s.Key != "take" && s.Key != "skip").Where(s => s.Value.Any()).ToDictionary(s => s.Key, s => s.Value.First());
            var users = await _users.Query(query, take, skip, cancellationToken);

            return users.Select(user => new UserViewModel { id = user.Id, user = user });
        }

        /// <summary>
        /// Gets an user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{id}")]
        public async Task<User?> Get(string id)
        {
            await using var scope = scene.CreateRequestScope();
            var _users = scope.Resolve<IUserService>();
            return await _users.GetUser(id);
        }

        /// <summary>
        /// Deletes an user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("{id}")]
        public async Task Delete(string id)
        {
            await using var scope = scene.CreateRequestScope();
            var _users = scope.Resolve<IUserService>();
            await _users.Delete(id);
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
            await using var scope = scene.CreateRequestScope();
            var _users = scope.Resolve<IUserService>();
            var user = await _users.GetUser(userId);
            if (user == null)
            {
                throw new ClientException("NotFound");
            }

          
            var auth = scope.Resolve<IAuthenticationService>();
            await auth.Unlink(user, provider);
        }

        /// <summary>
        /// kicks a player from the server.
        /// </summary>
        /// <param name="args">Kick options.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("_kick")]
        public async Task<ActionResult> Kick([FromBody] KickArguments args, CancellationToken cancellationToken)
        {
            if(args.reason == null)
            {
                return BadRequest("'reason' cannot be null.");
            }
            await using var scope = scene.CreateRequestScope();
            await scope.Resolve<IUserSessions>().KickUser(args.Ids, args.reason, cancellationToken);

            return Ok();
        }


    }

    /// <summary>
    /// Arguments passed to the kick API.
    /// </summary>
    public class KickArguments
    {
        /// <summary>
        /// Ids of the users to kick. '*' to kick all players.
        /// </summary>
        public IEnumerable<string> Ids { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Reason the player was kick out of the server.
        /// </summary>
        /// <remarks>
        /// *the reason is sent to the client.
        /// </remarks>
        public string reason { get; set; } = "adminRequest";
    }


    /// <summary>
    /// An user in the DB
    /// </summary>
    public class UserViewModel
    {
        /// <summary>
        /// Gets the id of the user.
        /// </summary>
        public string id { get; set; } = default!;

        /// <summary>
        /// Gets the user.
        /// </summary>
        public User user { get; internal set; } = default!;
    }
}
