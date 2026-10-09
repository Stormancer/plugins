using Microsoft.IdentityModel.Abstractions;
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

        public EOSAuthenticationEventHandler(
            ConfigurationMonitor<EOSConfigurationSection> configuration, 
            OAuthService oAuth, 
            IProfileService profiles,
            IEOSService eos)
        {
            _configuration = configuration;
            _oAuth = oAuth;
            _profiles = profiles;
            _eos = eos;
        }

        async Task IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationCompleteContext ctx, CancellationToken cancellationToken)
        {
            if (ctx.CurrentSession is null)
            {
                return;
            }
           
            var config = _configuration.Value;
            if (config.audience is null)
            {
                return;
            }

            string? userId = ctx.CurrentSession.User?.Id;
            if(userId is null)
            {
                return;
            }

            

            if (ctx.AuthParameters.Parameters.TryGetValue("eos.openID", out var value) && value == "true")
            {
                // Try getting the EOS product user id.
                if (!ctx.CurrentSession.Identities.ContainsKey("eos"))
                {
                    var productIds = await _eos.GetExternalAccounts([userId], "openid");

                    if (productIds.TryGetValue(userId, out var eosProductUserId) && eosProductUserId is not null)
                    {
                        ctx.CurrentSession.Identities.Add("eos", eosProductUserId);
                        SetMetadata(ctx, "eos.productUserId", eosProductUserId);
                    }
                }


                var session = ctx.CurrentSession.CreateView();

                string displayName = string.Empty;
                if (ctx.CurrentSession.User is not null)
                {
                    userId = ctx.CurrentSession.User.Id;
                    var profile = await _profiles.GetProfile(userId, new() { ["user"] = "summary" }, session, CancellationToken.None);
                    if (profile is not null && profile.TryGetValue("user", out var json) && json.TryGetValue("pseudo", out var pseudoJson))
                    {
                        displayName = pseudoJson.ToObject<string>() ?? string.Empty;

                    }

                }


                var claims = new Dictionary<string, object>
                {
                    ["sub"] = userId,
                    ["displayName"] = displayName
                };
                var jwt = await _oAuth.CreateToken(ctx.CurrentSession.CreateView(), config.audience, claims);

               
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
