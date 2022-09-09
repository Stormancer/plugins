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
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extension methods for DeviceIdentifier auth provider.
    /// </summary>
    public static class DeviceIdentifierAuthenticationConfigurationExtensions
    {
        /// <summary>
        /// Configures the device identifier auth provider.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <remarks>Usage on the client :
        /// authParams auth;
        /// auth.type = "deviceidentifier";
        /// auth.parameters["deviceidentifier"] = xxxx;
        /// </remarks>
        /// <returns></returns>
        public static UsersConfigurationBuilder ConfigureDeviceIdentifier(this UsersConfigurationBuilder config, Func<DeviceIdentifierConfigurationBuilder, DeviceIdentifierConfigurationBuilder> builder)
        {
            var b = new DeviceIdentifierConfigurationBuilder();

            b = builder(b);
            config.Settings[DeviceIdentifierConstants.PROVIDER_NAME] = JObject.FromObject(b);
            return config;
        }
    }
}

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Configures the device identifier auth provider.
    /// </summary>
    public class DeviceIdentifierConfigurationBuilder : AuthProviderConfigurationBuilderBase<DeviceIdentifierConfigurationBuilder>
    {
    }

    /// <summary>
    /// Device identifier constants.
    /// </summary>
    public static class DeviceIdentifierConstants
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public const string PROVIDER_NAME = "deviceidentifier";

        /// <summary>
        /// Claim path.
        /// </summary>
        public const string ClaimPath = "deviceid";
    }

    internal class DeviceIdentifierAuthenticationProvider : IAuthenticationProvider
    {
        private readonly ILogger _logger;
        private readonly IUserService _users;

        public string Type => DeviceIdentifierConstants.PROVIDER_NAME;

        public DeviceIdentifierAuthenticationProvider(IUserService users, ILogger logger)
        {
            _logger = logger;
            _users = users;
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            result["provider.deviceidentifier"] = "enabled";
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var pId = new PlatformId { Platform = DeviceIdentifierConstants.PROVIDER_NAME };

            if (!authenticationCtx.Parameters.TryGetValue("deviceidentifier", out var identifier))
            {
                return AuthenticationResult.CreateFailure("Device identifier must not be empty.", pId, authenticationCtx.Parameters);
            }

            var user = await _users.GetUserByClaim(DeviceIdentifierConstants.PROVIDER_NAME, DeviceIdentifierConstants.ClaimPath, identifier);

            if (user != null && authenticationCtx.CurrentSession?.User != null && authenticationCtx.CurrentSession.User.Id != user.Id)
            {
                return AuthenticationResult.CreateFailure("This device identifier is already linked to another account.", pId, authenticationCtx.Parameters);
            }

            if (user == null)
            {
                var uid = Guid.NewGuid().ToString("N");
                user = await _users.CreateUser(uid, JObject.FromObject(new { deviceidentifier = identifier }), DeviceIdentifierConstants.PROVIDER_NAME);

                user = await _users.AddAuthentication(user, DeviceIdentifierConstants.PROVIDER_NAME, claim => claim[DeviceIdentifierConstants.ClaimPath] = identifier, new Dictionary<string, string> { { DeviceIdentifierConstants.ClaimPath, identifier } });
            }
            else
            {
                if (user.LastPlatform != DeviceIdentifierConstants.PROVIDER_NAME)
                {
                    await _users.UpdateLastPlatform(user.Id, DeviceIdentifierConstants.PROVIDER_NAME);
                    user.LastPlatform = DeviceIdentifierConstants.PROVIDER_NAME;
                }
            }

            pId.PlatformUserId = user.Id;

            return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
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
            return _users.RemoveAuthentication(user, DeviceIdentifierConstants.PROVIDER_NAME);
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            return Task.FromResult<DateTime?>(null);
        }
    }
}

