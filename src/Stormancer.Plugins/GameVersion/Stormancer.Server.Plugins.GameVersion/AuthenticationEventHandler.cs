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

using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Users;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameVersion
{
    internal class AuthenticationEventHandler : IAuthenticationEventHandler
    {
        private readonly GameVersionService gameVersionService;
        private readonly IUserSessions _userSessions;
        private readonly ILogger _logger;

        public AuthenticationEventHandler(GameVersionService gameVersionService, IUserSessions userSessions, ILogger logger)
        {
            this.gameVersionService = gameVersionService;
            _userSessions = userSessions;
            _logger = logger;
        }

        public async Task OnLoggedIn(LoggedInCtx ctx)
        {
            if (ctx.AuthCtx.Parameters.TryGetValue("gameVersion.clientVersion", out var version))
            {
                if (version != null)
                {
                    await _userSessions.UpdateSessionData(ctx.Session.SessionId, "gameVersion.clientVersion", version, ctx.CancellationToken);
                }
            }

            return;
        }

        public Task OnLoggingIn(LoggingInCtx authenticationCtx)
        {
            var config = gameVersionService.CurrentConfiguration;
            if (!config.enableVersionChecking)
            {
                return Task.CompletedTask;
            }
            //Don't control the game version on "services" clients. Services clients include game server hosting agents.
            if (authenticationCtx.Context is string type && type == "service")
            {
                return Task.CompletedTask;
            }


           

            string? reason = null;
            if (authenticationCtx.AuthCtx.Parameters.TryGetValue("gameVersion.clientVersion", out var clientVersion))
            {
                if (config.AuthorizedVersions.Any())
                {
                    foreach (var version in config.AuthorizedVersions)
                    {
                        if (CheckClientVersionString(clientVersion,version))
                        {
                            return Task.CompletedTask;
                        }
                      
                    }

                    authenticationCtx.Reason = $"badGameVersion?mismatch&serverVersion={string.Join(",", config.AuthorizedVersions)}&clientVersion={clientVersion}";
                    authenticationCtx.HasError = true;
                    return Task.CompletedTask;
                }
                else if(config.version !=null)
                {
                    if (CheckClientVersionString(clientVersion,config.version))
                    {
                        return Task.CompletedTask;
                    }
                    else
                    {
                        authenticationCtx.Reason = $"badGameVersion?mismatch&serverVersion={config.version}&clientVersion={clientVersion}";
                        authenticationCtx.HasError = true;
                        return Task.CompletedTask;
                    }
                }
                else
                {
                    authenticationCtx.Reason = $"badGameVersion?mismatch&serverVersion=None&clientVersion={clientVersion}";
                    authenticationCtx.HasError = true;
                    return Task.CompletedTask;
                }
            }
            else
            {
                authenticationCtx.HasError = true;
               reason = $"badGameVersion?missingClientVersion";

            }

            return Task.CompletedTask;
        }


        private bool CheckClientVersionString(string clientVersion, string serverVersion)
        {
            if (serverVersion.EndsWith('*'))
            {

                if (!clientVersion.StartsWith(serverVersion.TrimEnd('*')))
                {
                    
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (clientVersion != serverVersion)
                {
                    
                    return false;

                }
                else
                {
                    return true;
                }
            }
        }
    }
}
