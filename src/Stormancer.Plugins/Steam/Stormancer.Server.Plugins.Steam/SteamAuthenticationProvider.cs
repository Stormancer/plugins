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
using Server.Plugins.Configuration;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Steam;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Users
{
    public class SteamAuthenticationProvider : IAuthenticationProvider, IUserSessionEventHandler
    {
        private ConcurrentDictionary<ulong, string> _vacSessions = new ConcurrentDictionary<ulong, string>();
        public const string PROVIDER_NAME = "steam";
        public const string ClaimPath = "steamid";
        private bool _vacEnabled = false;
        private ISteamUserTicketAuthenticator _authenticator;
        private ILogger _logger;
        private readonly IUserService _users;
        private readonly ISteamService _steam;

        public string Type => PROVIDER_NAME;

        public SteamAuthenticationProvider(IConfiguration config, ILogger logger, IUserService users, ISteamService steam)
        {
            _logger = logger;
            _users = users;
            _steam = steam;
            //config.SettingsChanged += (sender, e) => ApplyConfig(config);
            ApplyConfig(config);
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            result["provider.steamauthentication"] = "enabled";
        }

        private void ApplyConfig(IConfiguration config)
        {
            var steamConfig = config.Settings.steam;

            if (steamConfig?.usemockup != null && (bool)(steamConfig.usemockup))
            {
                _authenticator = new SteamUserTicketAuthenticatorMockup();
            }
            else
            {
                _authenticator = new SteamUserTicketAuthenticator(_steam);
            }
            _vacEnabled = steamConfig?.vac != null && (bool)steamConfig.vac;
        }

        public Task OnLoggedIn(LoginContext ctx)
        {
            return Task.FromResult(true);
        }

        public async Task OnLoggedOut(LogoutContext ctx)
        {
            var steamId = ctx.Session.User.GetSteamId();
            if (_vacSessions.TryRemove(steamId.Value, out string vacSessionId))
            {
                try
                {
                    await _steam.CloseVACSession(steamId.ToString(), vacSessionId);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"authenticator.steam", "Failed to close vac session for user '{steamId}'", ex);
                }
            }
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var pId = new PlatformId { Platform = PROVIDER_NAME };
            if (!authenticationCtx.Parameters.TryGetValue("ticket", out string ticket) || string.IsNullOrWhiteSpace(ticket))
            {
                return AuthenticationResult.CreateFailure("Steam session ticket must not be empty.", pId, authenticationCtx.Parameters);
            }
            try
            {
                var steamId = await _authenticator.AuthenticateUserTicket(ticket);

                if (!steamId.HasValue)
                {
                    return AuthenticationResult.CreateFailure("Invalid steam session ticket.", pId, authenticationCtx.Parameters);
                }
                pId.OnlineId = steamId.ToString();

                if (_vacEnabled)
                {
                    AuthenticationResult result = null;
                    string vacSessionId = null;
                    try
                    {
                        vacSessionId = await _steam.OpenVACSession(steamId.Value.ToString());
                        _vacSessions[steamId.Value] = vacSessionId;


                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "authenticator.steam", $"Failed to start VAC session for {steamId}", ex);
                        result = AuthenticationResult.CreateFailure($"Failed to start VAC session.", pId, authenticationCtx.Parameters);
                    }

                    try
                    {
                        if (!await _steam.RequestVACStatusForUser(steamId.Value.ToString(), vacSessionId))
                        {
                            result = AuthenticationResult.CreateFailure($"Connection refused by VAC.", pId, authenticationCtx.Parameters);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "authenticator.steam", $"Failed to check VAC status for  {steamId}", ex);
                        result = AuthenticationResult.CreateFailure($"Failed to check VAC status for user.", pId, authenticationCtx.Parameters);
                    }

                    if (result != null)//Failed
                    {
                        if (_vacSessions.TryRemove(steamId.Value, out vacSessionId))
                        {
                            try
                            {
                                await _steam.CloseVACSession(steamId.ToString(), vacSessionId);
                            }
                            catch (Exception ex)
                            {
                                _logger.Log(LogLevel.Error, $"authenticator.steam", $"Failed to close vac session for user '{steamId}'", ex);
                            }
                        }
                        return result;
                    }

                }
                var steamIdString = steamId.GetValueOrDefault().ToString();
                var user = await _users.GetUserByClaim(PROVIDER_NAME, ClaimPath, steamIdString);
                var playerSummary = await _steam.GetPlayerSummary(steamId.Value);
                if (user == null)
                {
                    var uid = Guid.NewGuid().ToString("N");

                    user = await _users.CreateUser(uid, JObject.FromObject(new { steamid = steamIdString, pseudo = playerSummary.personaname, avatar = playerSummary.avatarfull }));



                    user = await _users.AddAuthentication(user, PROVIDER_NAME, claim => claim[ClaimPath] = steamIdString, new Dictionary<string, string> { { ClaimPath, steamIdString } });
                }
                else
                {
                    user.UserData["pseudo"] = playerSummary.personaname;
                    user.UserData["avatar"] = playerSummary.avatarfull;
                    await _users.UpdateUserData(user.Id, user.UserData);
                }

                return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, "authenticator.steam", $"Steam authentication failed. Ticket : {ticket}", ex);
                return AuthenticationResult.CreateFailure($"Invalid steam session ticket.", pId, authenticationCtx.Parameters);
            }
        }

        public Task Setup(Dictionary<string, string> parameters)
        {
            throw new NotImplementedException();
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public async Task Unlink(User user)
        {
            if (user != null)
            {
                var steamId = (string)user.Auth[PROVIDER_NAME]?[ClaimPath];
                if (steamId == null)
                {
                    throw new ClientException($"authentication.unlink_failed?reason=not_linked&provider={PROVIDER_NAME}");
                }
                await _users.RemoveAuthentication(user, PROVIDER_NAME);
            }
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }

        public Task OnUpdatingUserHandle(UpdateUserHandleCtx ctx)
        {
            return Task.CompletedTask;
        }
    }
}

