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

using Newtonsoft.Json;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.DataProtection;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    class DedicatedServerAuthProvider : IAuthenticationProvider
    {
        public const string PROVIDER_NAME = "gameServer";
        private readonly IDataProtector dataProtector;

        public string Type => PROVIDER_NAME;
        public DedicatedServerAuthProvider(IDataProtector dataProtector)
        {
            this.dataProtector = dataProtector;
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            //result.Add("provider.dedicatedServer", "enabled");
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var token = authenticationCtx.Parameters["token"];


            try
            {
                var claims = System.Text.Json.JsonSerializer.Deserialize<GameServerAuthClaims>(Encoding.UTF8.GetString(await dataProtector.UnprotectBase64Url(token, "gameServer")));
                if(claims == null)
                {
                    return AuthenticationResult.CreateFailure("Invalid token : Null", new PlatformId { Platform = PROVIDER_NAME }, authenticationCtx.Parameters);
                }
                return AuthenticationResult.CreateSuccess(new User { Id = "ds-" + claims.GameServerId }, new PlatformId { PlatformUserId = claims.GameServerId, Platform = claims.ProviderType }, authenticationCtx.Parameters);
            }
            catch (Exception ex)
            {
                return AuthenticationResult.CreateFailure("Invalid token :" + ex.ToString(), new PlatformId { Platform = PROVIDER_NAME }, authenticationCtx.Parameters);
            }
        }

        public Task Setup(Dictionary<string, string> parameters, Session? session)
        {
            throw new NotSupportedException();
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task Unlink(User user)
        {
            throw new NotSupportedException();
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            return Task.FromResult<DateTime?>(null);
        }
    }


    class DevDedicatedServerAuthProvider : IAuthenticationProvider
    {
        public const string PROVIDER_NAME = DedicatedServerAuthProvider.PROVIDER_NAME + ".dev";


        public string Type => PROVIDER_NAME;
        public DevDedicatedServerAuthProvider()
        {

        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            //result.Add("provider.dedicatedServer", "enabled");
        }

        public Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {

            try
            {
                var gameServerId = Guid.NewGuid().ToString();
                return Task.FromResult(AuthenticationResult.CreateSuccess(new User { Id = "ds-" + gameServerId }, new PlatformId { PlatformUserId = gameServerId, Platform = PROVIDER_NAME }, authenticationCtx.Parameters));
            }
            catch (Exception ex)
            {
                return Task.FromResult(AuthenticationResult.CreateFailure("Invalid token :" + ex.ToString(), new PlatformId { Platform = PROVIDER_NAME }, authenticationCtx.Parameters));
            }
        }

        public Task Setup(Dictionary<string, string> parameters, Session? session)
        {
            throw new NotSupportedException();
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task Unlink(User user)
        {
            throw new NotSupportedException();
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            return Task.FromResult<DateTime?>(null);
        }
    }
}