using Microsoft.OpenApi.Writers;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Analytics
{
    internal class SessionsAnalyticsWorker
    {
        private readonly IAnalyticsService _analytics;
        private readonly ISceneHost _scene;

        public SessionsAnalyticsWorker(IAnalyticsService service, ISceneHost scene)
        {
            _analytics = service;
            _scene = scene;
           
        }


        public async Task Run(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (!cancellationToken.IsCancellationRequested)
            {

                await timer.WaitForNextTickAsync(cancellationToken);
                await using var scope = _scene.CreateRequestScope();
                try
                {
                    var userSessions = (UserSessions)scope.Resolve<IUserSessions>();
                    var groups = await userSessions.GetAuthenticatedUsersByDimensionsAsync();
                    foreach (var group in groups)
                    {
                        _analytics.Push("user", "sessions",
                            JObject.FromObject(group)
                            );
                    }
                }
                catch { }


            }
        }
    }
}
