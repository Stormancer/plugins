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
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extension methods for DeviceIdentifier auth provider.
    /// </summary>
    public static class SteamAuthenticationConfigurationExtensions
    {
        /// <summary>
        /// Configures steam authentication.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <remarks></remarks>
        /// <returns></returns>
        public static UsersConfigurationBuilder ConfigureSteam(this UsersConfigurationBuilder config, Func<Server.Plugins.Steam.SteamAuthConfigurationBuilder, Server.Plugins.Steam.SteamAuthConfigurationBuilder> builder)
        {
            var b = new Server.Plugins.Steam.SteamAuthConfigurationBuilder();

            b = builder(b);
            config.Settings[Server.Plugins.Steam.SteamConstants.PLATFORM_NAME] = JObject.FromObject(b);
            return config;
        }

    }
}

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Configures the steam authentication provider.
    /// </summary>
    public class SteamAuthConfigurationBuilder : AuthProviderConfigurationBuilderBase<SteamAuthConfigurationBuilder>
    {

        /// <summary>
        /// Gets or sets the default Steam authentication protocol.
        /// </summary>
        public string defaultAuthProtocol { get; set; } = "v0001";

        /// <summary>
        /// Sets the default steam authentication protocol used by the game.
        /// </summary>
        /// <param name="defaultAuthProtocol"></param>
        /// <returns></returns>
        /// <remarks>Defaults to <see cref="SteamAuthenticationProtocolVersion.V0001"/> for backward compatibility reasons.</remarks>
        public SteamAuthConfigurationBuilder DefaultAuthProtocol(SteamAuthenticationProtocolVersion defaultAuthProtocol)
        {
            this.defaultAuthProtocol = defaultAuthProtocol.ToString();
            return this;
        }

    }

    /// <summary>
    /// Steam authentication provider.
    /// </summary>
    class SteamAuthenticationProvider : IAuthenticationProvider, IUserSessionEventHandler
    {
        private ConcurrentDictionary<ulong, string> _vacSessions = new ConcurrentDictionary<ulong, string>();
    
        private readonly ConfigurationMonitor<SteamConfigurationSection> _configuration;
        private ILogger _logger;
        private readonly IUserService _users;
        private readonly ISteamService _steam;
        public string Type => SteamConstants.PLATFORM_NAME;

        /// <summary>
        /// Steam authentication provider constructor.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="users"></param>
        /// <param name="steam"></param>
        public SteamAuthenticationProvider(ConfigurationMonitor<SteamConfigurationSection> configuration, ILogger logger, IUserService users, ISteamService steam)
        {
            this._configuration = configuration;
            _logger = logger;
            _users = users;
            _steam = steam;

        }

        /// <summary>
        /// Add metadata.
        /// </summary>
        /// <param name="result"></param>
        public void AddMetadata(Dictionary<string, string> result)
        {
            result["provider.steamauthentication"] = "enabled";
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
            if (ctx.Session.User != null)
            {
                if (ctx.Session.User.TryGetSteamId(out var steamId) && ctx.Session.TryGetSteamAppId(out var appId) && _vacSessions.TryRemove(steamId, out var vacSessionId))
                {
                    try
                    {
                        await _steam.CloseVACSession(appId, steamId.ToString(), vacSessionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"authenticator.steam", "Failed to close vac session for user '{steamId}'", ex);
                    }
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
            AuthenticationResult? result = null;
            var pId = new PlatformId { Platform = SteamConstants.PLATFORM_NAME };
            if (!authenticationCtx.Parameters.TryGetValue("ticket", out var ticket) || string.IsNullOrWhiteSpace(ticket))
            {
                return AuthenticationResult.CreateFailure("Steam session ticket must not be empty.", pId, authenticationCtx.Parameters);
            }
            uint? appIdParam;
            if (authenticationCtx.Parameters.TryGetValue("appId", out var appIdStr))
            {
                appIdParam = uint.Parse(appIdStr);
            }
            else
            {
                appIdParam = null;
            }
            if (!authenticationCtx.Parameters.TryGetValue("version", out var versionStr))
            {
                versionStr = _configuration.Value.defaultAuthProtocol;
            }
            try
            {
                var (steamId, appId) = await _steam.AuthenticateUserTicket(ticket, appIdParam, versionStr.Equals("v1", StringComparison.InvariantCultureIgnoreCase) ? SteamAuthenticationProtocolVersion.V1 : SteamAuthenticationProtocolVersion.V0001);


                pId.PlatformUserId = steamId.ToString()!;

                if (_configuration.Value.vac)
                {

                    string vacSessionId;
                    try
                    {
                        vacSessionId = await _steam.OpenVACSession(appId, steamId.ToString());
                        _vacSessions[steamId] = vacSessionId;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "authenticator.steam", $"Failed to start VAC session for {steamId}", ex);
                        return AuthenticationResult.CreateFailure($"Failed to start VAC session.", pId, authenticationCtx.Parameters);
                    }

                    try
                    {
                        if (!await _steam.RequestVACStatusForUser(appId, steamId.ToString(), vacSessionId))
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
                        if (_vacSessions.TryRemove(steamId, out _))
                        {
                            try
                            {
                                await _steam.CloseVACSession(appId, steamId.ToString()!, vacSessionId);
                            }
                            catch (Exception ex)
                            {
                                _logger.Log(LogLevel.Error, $"authenticator.steam", $"Failed to close vac session for user '{steamId}'", ex);
                            }
                        }
                        return result;
                    }
                }

                var steamIdString = steamId.ToString();
                var user = await _users.GetUserByIdentity(SteamConstants.PLATFORM_NAME,  steamIdString);
                var playerSummary = await _steam.GetPlayerSummary(steamId);
                if (user == null)
                {
                    var uid = Guid.NewGuid().ToString("N");

                    user = await _users.CreateUser(uid, JObject.FromObject(new Dictionary<string, string> {
                        { SteamConstants.ClaimPath, steamIdString },
                        { "pseudo", playerSummary?.personaname ?? "" }
                    }), SteamConstants.PLATFORM_NAME);

                    user = await _users.AddAuthentication(user, SteamConstants.PLATFORM_NAME,steamIdString, claim => claim[SteamConstants.ClaimPath] = steamIdString);
                }
                else
                {
                    if (user.LastPlatform != SteamConstants.PLATFORM_NAME)
                    {
                        await _users.UpdateLastPlatform(user.Id, SteamConstants.PLATFORM_NAME);
                        user.LastPlatform = SteamConstants.PLATFORM_NAME;
                    }

                    //if (playerSummary != null)
                    //{
                    //    var userDataSteamId = user.UserData[SteamConstants.STEAM_ID];
                    //    if (userDataSteamId == null || userDataSteamId.ToObject<ulong>() != playerSummary.steamid)
                    //    {
                    //        user.UserData[SteamConstants.STEAM_ID] = playerSummary.steamid;
                    //        updateUserData = true;
                    //    }

                    //    var userDataPseudo = user.UserData["pseudo"];
                    //    if (userDataPseudo == null || userDataPseudo.ToString() != playerSummary.personaname)
                    //    {
                    //        user.UserData["pseudo"] = playerSummary.personaname;
                    //        updateUserData = true;
                    //    }
                    //}
                    //if (updateUserData)
                    //{
                    //    await _users.UpdateUserData(user.Id, user.UserData);
                    //}
                }


                result = AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
                result.OnSessionUpdated += (SessionRecord session) => { session.SessionData["steam.appId"] = BitConverter.GetBytes(appId); };
                return result;
            }
            catch (SteamException)
            {
                //Wait 1s before sending the response to slow down bad behaving clients.
                await Task.Delay(1000);
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
                var steamId = (string?)user.Auth[SteamConstants.PLATFORM_NAME]?[SteamConstants.ClaimPath];
                if (steamId == null)
                {
                    throw new ClientException($"authentication.unlink_failed?reason=not_linked&provider={SteamConstants.PLATFORM_NAME}");
                }
                await _users.RemoveAuthentication(user, SteamConstants.PLATFORM_NAME);
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
