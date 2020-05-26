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

using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameVersion
{
    internal class AuthenticationEventHandler : IAuthenticationEventHandler
    {
        private readonly GameVersionService gameVersionService;
        private readonly IUserSessions _userSessions;

        public AuthenticationEventHandler(GameVersionService gameVersionService, IUserSessions userSessions)
        {
            this.gameVersionService = gameVersionService;
            _userSessions = userSessions;
        }

        public async Task OnLoggedIn(LoggedInCtx ctx)
        {
            if (ctx.AuthCtx.Parameters.TryGetValue("gameVersion.clientVersion", out var version))
            {
                if (version != null)
                {
                    await _userSessions.UpdateSessionData(ctx.Session.SessionId, "gameVersion.clientVersion", version);
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

            if (authenticationCtx.AuthCtx.Parameters.TryGetValue("gameVersion.clientVersion", out var version))
            {
                if (version != gameVersionService.CurrentGameVersion)
                {
                    authenticationCtx.HasError = true;
                    authenticationCtx.Reason = $"badGameVersion?mismatch&serverVersion={gameVersionService.CurrentGameVersion}";
                }
            }
            else
            {
                authenticationCtx.HasError = true;
                authenticationCtx.Reason = $"badGameVersion?absent&serverVersion={gameVersionService.CurrentGameVersion}";
            }

            return Task.CompletedTask;
        }
    }
}
