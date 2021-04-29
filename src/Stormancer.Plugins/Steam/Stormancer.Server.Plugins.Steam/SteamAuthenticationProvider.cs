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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Steam authentication provider.
    /// </summary>
    class SteamAuthenticationProvider : IAuthenticationProvider, IUserSessionEventHandler
    {
        private ConcurrentDictionary<ulong, string> _vacSessions = new ConcurrentDictionary<ulong, string>();
        private bool _vacEnabled = false;
        private ISteamUserTicketAuthenticator _authenticator;
        private ILogger _logger;
        private readonly IUserService _users;
        private readonly ISteamService _steam;
        public string Type => SteamConstants.PROVIDER_NAME;

        /// <summary>
        /// Steam authentication provider constructor.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="users"></param>
        /// <param name="steam"></param>
        /// <param name="authenticator"></param>
        public SteamAuthenticationProvider(IConfiguration configuration, ILogger logger, IUserService users, ISteamService steam, ISteamUserTicketAuthenticator authenticator)
        {
            _logger = logger;
            _users = users;
            _steam = steam;
            _authenticator = authenticator;

            ApplyConfig(configuration.Settings);

        }

        /// <summary>
        /// Add metadata.
        /// </summary>
        /// <param name="result"></param>
        public void AddMetadata(Dictionary<string, string> result)
        {
            result["provider.steamauthentication"] = "enabled";
        }

        private void ApplyConfig(dynamic config)
        {
            _vacEnabled = config?.steam?.vac ?? false;
        }

        /// <summary>
        /// On logged in.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnLoggedIn(LoginContext ctx)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// On logged out.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task OnLoggedOut(LogoutContext ctx)
        {
            var steamId = ctx.Session.User.GetSteamId();
            if (steamId != null && _vacSessions.TryRemove(steamId.Value, out var vacSessionId))
            {
                try
                {
                    await _steam.CloseVACSession(steamId.ToString()!, vacSessionId);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"authenticator.steam", "Failed to close vac session for user '{steamId}'", ex);
                }
            }
        }

        /// <summary>
        /// Authenticate.
        /// </summary>
        /// <param name="authenticationCtx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var pId = new PlatformId { Platform = SteamConstants.PROVIDER_NAME };
            if (!authenticationCtx.Parameters.TryGetValue("ticket", out var ticket) || string.IsNullOrWhiteSpace(ticket))
            {
                return AuthenticationResult.CreateFailure("Steam session ticket must not be empty.", pId, authenticationCtx.Parameters);
            }

            try
            {
                var steamId = await _authenticator.AuthenticateUserTicket(ticket);

                if (steamId == null)
                {
                    return AuthenticationResult.CreateFailure("Invalid steam session ticket.", pId, authenticationCtx.Parameters);
                }
                pId.OnlineId = steamId.ToString()!;

                if (_vacEnabled)
                {
                    AuthenticationResult? result = null;
                    string vacSessionId;
                    try
                    {
                        vacSessionId = await _steam.OpenVACSession(steamId.Value.ToString());
                        _vacSessions[steamId.Value] = vacSessionId;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "authenticator.steam", $"Failed to start VAC session for {steamId}", ex);
                        return AuthenticationResult.CreateFailure($"Failed to start VAC session.", pId, authenticationCtx.Parameters);
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

                    if (result != null) // Failed
                    {
                        if (_vacSessions.TryRemove(steamId.Value, out _))
                        {
                            try
                            {
                                await _steam.CloseVACSession(steamId.ToString()!, vacSessionId);
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
                var user = await _users.GetUserByClaim(SteamConstants.PROVIDER_NAME, SteamConstants.ClaimPath, steamIdString);
                var playerSummary = await _steam.GetPlayerSummary(steamId.Value);
                if (user == null)
                {
                    var uid = Guid.NewGuid().ToString("N");

                    user = await _users.CreateUser(uid, JObject.FromObject(new Dictionary<string, string> { { SteamConstants.ClaimPath, steamIdString }, { "pseudo", playerSummary?.personaname ?? "" }, { "avatar", playerSummary?.avatar ?? "" }, { "steamProfileUrl", playerSummary?.profileurl ?? "" } }));

                    user = await _users.AddAuthentication(user, SteamConstants.PROVIDER_NAME, claim => claim[SteamConstants.ClaimPath] = steamIdString, new Dictionary<string, string> { { SteamConstants.ClaimPath, steamIdString } });
                }
                else
                {
                    bool updateUserData = false;
                    if (playerSummary != null)
                    {
                        var userDataSteamId = user.UserData[SteamConstants.STEAM_ID];
                        if (userDataSteamId == null || userDataSteamId.ToObject<ulong>() != playerSummary.steamid)
                        {
                            user.UserData[SteamConstants.STEAM_ID] = playerSummary.steamid;
                            updateUserData = true;
                        }
                        var userDataPseudo = user.UserData["pseudo"];
                        if (userDataPseudo == null || userDataPseudo.ToString() != playerSummary.personaname)
                        {
                            user.UserData["pseudo"] = playerSummary.personaname;
                            updateUserData = true;
                        }
                        var userDataAvatar = user.UserData["avatar"];
                        if (userDataAvatar == null || userDataAvatar.ToString() != playerSummary.avatar)
                        {
                            user.UserData["avatar"] = playerSummary.avatar;
                            updateUserData = true;
                        }
                        var userDataProfileUrl = user.UserData["steamProfileUrl"];
                        if (userDataProfileUrl == null || userDataProfileUrl.ToString() != playerSummary.profileurl)
                        {
                            user.UserData["steamProfileUrl"] = playerSummary.profileurl;
                            updateUserData = true;
                        }
                    }
                    if (updateUserData)
                    {
                        await _users.UpdateUserData(user.Id, user.UserData);
                    }
                }

                return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
            }
            catch (SteamException)
            {
                return AuthenticationResult.CreateFailure($"Authentication refused by steam.", pId, authenticationCtx.Parameters);

            }
        }

        /// <summary>
        /// Setup.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public Task Setup(Dictionary<string, string> parameters, Session? session)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// on get status.
        /// </summary>
        /// <param name="status"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unlink claim from user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task Unlink(User user)
        {
            if (user != null)
            {
                var steamId = (string?)user.Auth[SteamConstants.PROVIDER_NAME]?[SteamConstants.ClaimPath];
                if (steamId == null)
                {
                    throw new ClientException($"authentication.unlink_failed?reason=not_linked&provider={SteamConstants.PROVIDER_NAME}");
                }
                await _users.RemoveAuthentication(user, SteamConstants.PROVIDER_NAME);
            }
        }

        /// <summary>
        /// Renew credentials.
        /// </summary>
        /// <param name="authenticationContext"></param>
        /// <returns></returns>
        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// On updating user handle.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnUpdatingUserHandle(UpdateUserHandleCtx ctx)
        {
            return Task.CompletedTask;
        }
    }
}
