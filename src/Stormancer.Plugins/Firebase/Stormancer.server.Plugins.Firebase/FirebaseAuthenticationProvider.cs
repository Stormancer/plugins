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
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.server.Plugins.Firebase
{
    class FirebaseAuthenticationProvider : IAuthenticationProvider
    {
        private const string ClaimPath = "uid";
        private readonly IUserService users;

        public string Type => "firebase";

        public FirebaseAuthenticationProvider(IUserService users)
        {
            this.users = users;
        }

        public void AddMetadata(Dictionary<string, string> result)
        {

        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            if (!authenticationCtx.Parameters.TryGetValue("token", out var token))
            {
                return AuthenticationResult.CreateFailure("A firebase authentication request must contain a 'token' parameter containing the user idToken", PlatformId.Unknown, authenticationCtx.Parameters);
            }

            try
            {
                var t = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);

                var firebaseUid = t.Uid;
                var user = await users.GetUserByClaim(Type, ClaimPath, firebaseUid);
                if (user == null)
                {
                    var uid = Guid.NewGuid().ToString("N");
                    user = await users.CreateUser(uid, JObject.FromObject(new { uid = firebaseUid }));
                    user = await users.AddAuthentication(user, Type, claim => claim[ClaimPath] = firebaseUid, new Dictionary<string, string> { { ClaimPath, firebaseUid } });
                }

                var pId = new PlatformId { Platform = Type, PlatformUserId = firebaseUid };
              
                return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
            }
            catch (FirebaseAdmin.Auth.FirebaseAuthException)
            {
                return AuthenticationResult.CreateFailure("Failed to validate idToken", PlatformId.Unknown, authenticationCtx.Parameters);
            }
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }

        public Task Setup(Dictionary<string, string> parameters, Session? session)
        {
            throw new ClientException("notSupported");
        }

        public Task Unlink(User user)
        {
            return users.RemoveAuthentication(user, Type);

        }
    }
}
