using Microsoft.AspNetCore.Cors.Infrastructure;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Users.OAuth;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Xsolla
{
    internal class XSollaAuthenticationEventHandler : IAuthenticationEventHandler
    {

        private readonly ConfigurationMonitor<XsollaConfigurationSection> _configuration;
        private readonly XsollaService _xsolla;
        private readonly IUserService _users;
        private readonly ILogger _logger;

        public XSollaAuthenticationEventHandler(
            ConfigurationMonitor<XsollaConfigurationSection> configuration,
            XsollaService xsolla,
            IUserService users,
            ILogger logger)
        {
            _configuration = configuration;
           
            _xsolla = xsolla;
            _users = users;
            _logger = logger;
        }

        async Task IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationCompleteContext ctx, CancellationToken cancellationToken)
        {
            var config = _configuration.Value;
            if (config.ProjectId == null || config.ClientId == null)
            {
                return;
            }

            if (ctx.Result.AuthenticatedUser is null)
            {
                return;
            }


            SetMetadata(ctx, "xsolla.loginProject", config.ProjectId);
            
           

            var user = ctx.Result.AuthenticatedUser;
            string userId = ctx.Result.AuthenticatedUser.Id;

            if (user.Identities.TryGetValue($"xsolla.{config.ProjectId}", out var serverCustomId)) //User linked to an XSolla account.
            {
                var jwt = await _xsolla.AuthByCustomId(serverCustomId);
                if (jwt != null)
                {
                    SetMetadata(ctx, "xsolla.jwt", jwt);
                    return;
                }

                //The JWT is null, the XSolla account was not found.
                await _users.RemoveAuthentication(user, $"xsolla.{config.ProjectId}");
            }


          
            if (ctx.AuthParameters.Parameters.TryGetValue("xsolla.jwt", out var value))
            {
                // the client provided an auth token. We need to link the xsolla account to the user.               
                var xsollaUser = await _xsolla.GetUserByToken(value);
               
                if(xsollaUser !=null)
                {
                    var externalId = xsollaUser.ExternalId;
                    if (xsollaUser.ExternalId == null)
                    {
                        await _xsolla.LinkUserWithExternalId(new XsollaUserId(xsollaUser.Id), user.Id);
                        externalId = user.Id;
                    }

                    if (externalId != userId)
                    {                       
                        ctx.Result.Success = false;
                        ctx.Result.ReasonMsg = "xsolla.account_linked_to_another_user";
                        return;
                    }

                    await _users.AddAuthentication(user, $"xsolla.{config.ProjectId}", user.Id, claims => claims[$"xsolla.{config.ProjectId}"] = externalId);
                }
            }


        }

        private void SetMetadata(AuthenticationCompleteContext ctx, string key, string value)
        {
            if (ctx.Result.Metadata == null)
            {
                ctx.Result.Metadata = new();
            }
            ctx.Result.Metadata[key] = value;
        }
    }
}
