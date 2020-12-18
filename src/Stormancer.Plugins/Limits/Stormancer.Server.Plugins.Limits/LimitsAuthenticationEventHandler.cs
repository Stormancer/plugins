using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    class LimitsAuthenticationEventHandler : IAuthenticationEventHandler, IUserSessionEventHandler
    {
        private readonly Limits limits;
        private readonly Func<IEnumerable<ILimitsEventHandler>> handlers;
        private readonly ILogger logger;
        private readonly ISerializer serializer;

        public LimitsAuthenticationEventHandler(Limits limits, Func<IEnumerable<ILimitsEventHandler>> handlers, ILogger logger, ISerializer serializer)
        {
            this.limits = limits;
            this.handlers = handlers;
            this.logger = logger;
            this.serializer = serializer;
        }

        async Task IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationCompleteContext ctx, CancellationToken cancellationToken)
        {
            try
            {
                if (!ctx.Result.Success)
                {
                    return;
                }

                //A session already exist: The user is already connected.
                if (ctx.CurrentSession != null)
                {
                    return;
                }

                var config = limits.Config;
              

                var applyUserLimitCtx = new OnApplyingUserLimitContext(ctx);
                await handlers().RunEventHandler(h => h.OnApplyingUserLimit(applyUserLimitCtx), ex => logger.Log(LogLevel.Error, "limits", $"An error occured while running {nameof(ILimitsEventHandler.OnApplyingUserLimit)}.", ex));

                if (!applyUserLimitCtx.ApplyUserLimit)
                {
                    using var stream = new MemoryStream();
                    serializer.Serialize(true, stream);
                    ctx.Result.OnSessionUpdated += (SessionRecord s) => { s.SessionData["skipQueue"] = stream.ToArray(); };
                    return;
                }
                var success = await limits.WaitForEntryAsync(ctx.Peer, ctx.Result.AuthenticatedId, cancellationToken);

                if (!success)
                {
                    ctx.Result.Success = false;
                    ctx.Result.ReasonMsg = "limit";
                }
            }
            catch (OperationCanceledException)
            {
                ctx.Result.Success = false;
                ctx.Result.ReasonMsg = "canceled";
            }

        }

        Task IUserSessionEventHandler.OnLoggedOut(LogoutContext ctx)
        {
            if (ctx.Session != null)
            {
                if (!ctx.Session.GetSessionValue<bool>("limits.skipQueue", serializer))
                {
                    limits.Logout(ctx.Session.SessionId, ctx.Session.User?.Id, ctx.Reason);
                }
            }
            return Task.CompletedTask;
        }
    }
}
