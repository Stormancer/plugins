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
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Stormancer
{
    /// <summary>
    /// Extension methods for LoginPassword auth provider.
    /// </summary>
    public static class LoginPasswordAuthenticationConfigurationExtensions
    {
        /// <summary>
        /// Configures the device identifier auth provider.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <remarks></remarks>
        /// <returns></returns>
        public static UsersConfigurationBuilder ConfigureLoginPassword(this UsersConfigurationBuilder config, Func<LoginPasswordConfigurationBuilder, LoginPasswordConfigurationBuilder> builder)
        {
            var b = new LoginPasswordConfigurationBuilder();

            b = builder(b);
            config.Settings[LoginPasswordAuthenticationProvider.PROVIDER_NAME] = JObject.FromObject(b);
            return config;
        }

    }

}

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Configures the device identifier auth provider.
    /// </summary>
    public class LoginPasswordConfigurationBuilder : AuthProviderConfigurationBuilderBase<LoginPasswordConfigurationBuilder>
    {
        /// <summary>
        /// True if emails are mandatory.
        /// </summary>
        public bool requiresEmail { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="require"></param>
        /// <returns></returns>
        public LoginPasswordConfigurationBuilder RequiresEmail(bool require = true)
        {
            requiresEmail = true;
            return this;
        }
    }

    internal class LoginPasswordAuthRecord
    {
        public string? email { get; set; }

        public string? login { get; set; }
        public byte[]? salt { get; set; }
        public int iterations { get; set; }
        public byte[]? derivedKey { get; set; }
        public string? algorithm { get; set; }

    }

    class LoginPasswordAuthenticationProvider : IAuthenticationProvider
    {

        public const string PROVIDER_NAME = "loginPassword";
        public const string ClaimPath = "loginpassword";
        private readonly IConfiguration config;
        private readonly IUserService users;
        private readonly IUserSessions sessions;
        private readonly ILogger logger;

        public LoginPasswordAuthenticationProvider(IConfiguration config, IUserService users, IUserSessions sessions, ILogger logger)
        {
            this.config = config;
            this.users = users;
            this.sessions = sessions;
            this.logger = logger;
        }

        public string Type => PROVIDER_NAME;

        public bool IsEnabled => ((bool?)config.Settings.auth?.loginPassword?.enabled) ?? false;
        public bool RequiresEmail => ((bool?)config.Settings.auth?.loginPassword?.requiresEmail) ?? true;
        public void AddMetadata(Dictionary<string, string> result)
        {
            if (IsEnabled)
            {
                result.Add("provider.loginPassword", "enabled");
            }
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {

            var pId = new PlatformId { Platform = PROVIDER_NAME };
            if (!authenticationCtx.Parameters.TryGetValue("username", out var login))
            {
                return AuthenticationResult.CreateFailure("loginPassword.login.missingArgument?id=login", pId, authenticationCtx.Parameters);
            }

            if (!authenticationCtx.Parameters.TryGetValue("password", out var password))
            {
                return AuthenticationResult.CreateFailure("loginPassword.login.missingArgument?id=password", pId, authenticationCtx.Parameters);
            }
            pId.PlatformUserId = login;

            var user = await users.GetUserByClaim(PROVIDER_NAME, ClaimPath + ".login", login);

            

            if (user == null)
            {
                logger.Log(LogLevel.Trace, "auth.loginPassword", "Failed to find user for login.", new { login });
                return AuthenticationResult.CreateFailure("loginPassword.login.invalidCredentials", pId, authenticationCtx.Parameters);
            }
            var authParams = user.Auth[PROVIDER_NAME]?[ClaimPath]?.ToObject<LoginPasswordAuthRecord>();

            if (authParams == null)
            {
                logger.Log(LogLevel.Trace, "auth.loginPassword", $"User {user.Id} doesn't have login password configured.", new { login });
                return AuthenticationResult.CreateFailure("loginPassword.login.invalidCredentials", pId, authenticationCtx.Parameters);
            }

            
            var derivedKey = DeriveKey(password, authParams.iterations, authParams.salt!, new HashAlgorithmName(authParams.algorithm));

            if (!BytesEqual(derivedKey, authParams.derivedKey!))
            {
                logger.Log(LogLevel.Trace, "auth.loginPassword", "Invalid password.", new { login });
                return AuthenticationResult.CreateFailure("loginPassword.login.invalidCredentials", pId, authenticationCtx.Parameters);
            }


            return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public async Task Setup(Dictionary<string, string> parameters, Session? session, CancellationToken cancellationToken)
        {
            var pId = new PlatformId { Platform = PROVIDER_NAME };
            if (!parameters.TryGetValue("username", out var login))
            {
                throw new ClientException("auth.loginPassword.create.missingParameter?id=login");
            }

            if (!parameters.TryGetValue("password", out var password))
            {
                throw new ClientException("auth.loginPassword.create.missingParameter?id=password");
            }

            if (!parameters.TryGetValue("email", out var email) && RequiresEmail)
            {
                throw new ClientException("auth.loginPassword.create.missingParameter?id=email");
            }

            pId.PlatformUserId = login;

            var user = await users.GetUserByClaim(PROVIDER_NAME, ClaimPath + ".login", login);
            if (user != null)
            {
                throw new ClientException("auth.loginPassword.create.accountAlreadyExist");
            }


            if (user == null)
            {
                string uid;
                if (session != null)
                {
                    user = session.User;
                }

                if(user !=null)
                {
                    uid = user.Id;
                }
                else
                {
                    uid = Guid.NewGuid().ToString("N");
                    user = await users.CreateUser(uid, JObject.FromObject(new { login, email = email ?? "" }));
                }
                     
                if (parameters.TryGetValue("pseudo", out var pseudo))
                {
                    await sessions.UpdateUserHandle(uid, pseudo, true,cancellationToken);
                    user = await users.GetUser(uid);
                }
                var hash = DeriveKey(password, out var iterations, out var salt, out var algorithm);

                Debug.Assert(user != null);

                user = await users.AddAuthentication(user, PROVIDER_NAME, claim =>
                {
                    claim[ClaimPath] = JObject.FromObject(new LoginPasswordAuthRecord
                    {
                        email = email,
                        login = login,
                        salt = salt,
                        iterations = iterations,
                        algorithm = algorithm.Name,
                        derivedKey = hash
                    });
                }, new Dictionary<string, string> { { ClaimPath, login } });
               
            }
         
        }

        private static byte[] DeriveKey(string password, out int iterations, out byte[] salt,
                                out HashAlgorithmName algorithm)
        {
            iterations = 100000;
            algorithm = HashAlgorithmName.SHA256;

            const int SaltSize = 32;
            const int DerivedValueSize = 32;

            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, SaltSize,
                                                                      iterations, algorithm))
            {
                salt = pbkdf2.Salt;
                return pbkdf2.GetBytes(DerivedValueSize);
            }
        }

        static bool BytesEqual(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }

        public static byte[] DeriveKey(string password, int iterations, byte[] salt, HashAlgorithmName algorithm)
        {
            const int DerivedValueSize = 32;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, algorithm))
            {
                return pbkdf2.GetBytes(DerivedValueSize);
            }
        }

        public Task Unlink(User user)
        {
            return users.RemoveAuthentication(user, PROVIDER_NAME);
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }
    }
}

