using Microsoft.IdentityModel.Abstractions;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Users.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    internal class EOSAuthenticationEventHandler : IAuthenticationEventHandler
    {
        private readonly ConfigurationMonitor<EOSConfigurationSection> _configuration;
        private readonly OAuthService _oAuth;
        private readonly IProfileService _profiles;
        private readonly IEOSService _eos;
        private readonly IUserService _users;
        private readonly ILogger _logger;

        public EOSAuthenticationEventHandler(
            ConfigurationMonitor<EOSConfigurationSection> configuration, 
            OAuthService oAuth, 
            IProfileService profiles,
            IEOSService eos,
            IUserService users,
            ILogger logger)
        {
            _configuration = configuration;
            _oAuth = oAuth;
            _profiles = profiles;
            _eos = eos;
            _users = users;
            _logger = logger;
        }

        async Task IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationCompleteContext ctx, CancellationToken cancellationToken)
        {
            if (ctx.Result.AuthenticatedUser is null)
            {
                return;
            }
           
            var config = _configuration.Value;
            if (config.audience is null)
            {
                return;
            }
            var user = ctx.Result.AuthenticatedUser;
            string userId = ctx.Result.AuthenticatedUser.Id;


            
            if (ctx.AuthParameters.Parameters.TryGetValue("eos.openID", out var value) && value == "true")
            {
                try
                {
                    // Try getting the EOS product user id.
                    if (!ctx.Result.AuthenticatedUser.Identities.ContainsKey("eos"))
                    {
                        var productIds = await _eos.GetExternalAccounts([userId], "openid");

                        if (productIds.TryGetValue(userId, out var eosProductUserId) && eosProductUserId is not null)
                        {
                            
                                ctx.Result.AuthenticatedUser.Identities.Add("eos", eosProductUserId);
                                await _users.AddAuthentication(user, "eos", eosProductUserId, claim => claim["eos"] = eosProductUserId);
                            
                           
                        }
                    }
                    if(ctx.Result.AuthenticatedUser.Identities.TryGetValue("eos",out var id))
                    {
                        SetMetadata(ctx, "eos.productUserId", id);
                    }
                   
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Info, "eos", "Failed getting the current product user id", ex);
                }


                var claims = new Dictionary<string, object>
                {
                    ["sub"] = userId
                };
                var jwt = await _oAuth.CreateToken(config.audience, claims);

               
                SetMetadata(ctx,"eos.jwt", jwt);

            }


        }

        private void SetMetadata(AuthenticationCompleteContext ctx,string key, string value)
        {
            if (ctx.Result.Metadata == null)
            {
                ctx.Result.Metadata = new();
            }
            ctx.Result.Metadata[key] = value;
        }
    }
}
