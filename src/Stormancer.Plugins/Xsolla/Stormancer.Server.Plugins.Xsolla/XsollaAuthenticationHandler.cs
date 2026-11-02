using Microsoft.AspNetCore.Cors.Infrastructure;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Users.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public string XsollaProvider => $"xsolla.{_configuration.Value.ProjectId}";

        async Task IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationCompleteContext ctx, CancellationToken cancellationToken)
        {
            var config = _configuration.Value;
            if (!config.Enabled || config.ProjectId == null || config.ClientId == null)
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

            if (user.Identities.TryGetValue(XsollaProvider, out var xsollaId)) //User linked to an XSolla account.
            {
                if (xsollaId != userId)
                {
                    var jwt = await _xsolla.AuthByCustomId(xsollaId);
                    if (jwt != null)
                    {
                        SetMetadata(ctx, "xsolla.jwt", jwt);
                        return;
                    }
                }

                //The JWT is null (the XSolla account was not found) or the account is using the old way of linking to XSolla
                await _users.RemoveAuthentication(user, XsollaProvider);
            }



            if (ctx.AuthParameters.Parameters.TryGetValue("xsolla.jwt", out var value))
            {
                // the client provided an auth token. We need to link the xsolla account to the user.               
                var xsollaUser = await _xsolla.GetUserByToken(value);

                if (xsollaUser != null)
                {
                    var previousUser = await _users.GetUserByIdentity(XsollaProvider, xsollaUser.Id);
                    var xsollaUserId = new XsollaUserId(xsollaUser.Id);

                    if (previousUser != null && previousUser.Id != userId)
                    {
                        List<XsollaGroup> groups = [];
                        try
                        {
                            groups = await _xsolla.GetUserGroups(xsollaUserId);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(LogLevel.Error, "xsolla", $"Failed to get user groups for user {xsollaUserId.Value}", ex);
                        }

                        if (groups.All(g => !_configuration.Value.AllowMultipleLinkingFor.Contains(g.Name)))
                        {
                            ctx.Result.Success = false;
                            ctx.Result.ReasonMsg = "xsolla.account_linked_to_another_user";
                            return;
                        }
                    }

                    await _xsolla.UpdateServerCustomId(xsollaUserId, xsollaUser.Id);

                    await _users.AddAuthentication(user, XsollaProvider, xsollaUser.Id, claims => claims[XsollaProvider] = xsollaUser.Id);
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
