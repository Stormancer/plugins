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
            if (!gameVersionService.CheckClientVersion)
            {
                return Task.CompletedTask;
            }
            //Don't control the gameversion on "services" clients. Services clients include gameserver hosting agents.
            if(authenticationCtx.AuthCtx.CurrentSession.SessionData.TryGetValue("stormancer.type", out var bytes) && System.Text.Encoding.UTF8.GetString(bytes) == "service")
            {
                return Task.CompletedTask;
            }

            var serverVersion = gameVersionService.CurrentGameVersion;

            if (authenticationCtx.AuthCtx.Parameters.TryGetValue("gameVersion.clientVersion", out var clientVersion))
            {
                if (serverVersion.EndsWith('*'))
                {
                   
                    if(!clientVersion.StartsWith(serverVersion.TrimEnd('*')))
                    {
                        authenticationCtx.HasError = true;
                        authenticationCtx.Reason = $"badGameVersion?mismatch&serverVersion={serverVersion}&clientVersion={clientVersion}";
                        _logger.Log(LogLevel.Trace, "GameVersion", "Client tried to connect with an outdated game version.", new { clientVersion, serverVersion });
                    }
                }
                else
                {
                    if (clientVersion != serverVersion)
                    {
                        authenticationCtx.HasError = true;
                        authenticationCtx.Reason = $"badGameVersion?mismatch&serverVersion={serverVersion}&clientVersion={clientVersion}";
                        _logger.Log(LogLevel.Trace, "GameVersion", "Client tried to connect with an outdated game version.", new { clientVersion, serverVersion });
                    }
                }
            }
            else
            {
                authenticationCtx.HasError = true;
                authenticationCtx.Reason = $"badGameVersion?absent&serverVersion={serverVersion}";
                _logger.Log(LogLevel.Trace, "GameVersion", "Client tried to connect without game version.", new { serverVersion });
            }

            return Task.CompletedTask;
        }
    }
}
