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
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Threading;
using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    class LocatorController : ControllerBase
    {

        private readonly IServiceLocator _locator;
        private readonly IUserSessions _sessions;
        private readonly ILogger logger;

        public LocatorController(IServiceLocator locator, IUserSessions sessions, ILogger logger)
        {
            _locator = locator;
            _sessions = sessions;
            this.logger = logger;
        }
        private Task<Session> TryGetSession(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            //Retry later if we fail to get the user session.
            return TaskHelper.Retry(async (peer, cancellationToken) =>
            {
                var session = await _sessions.GetSession(peer, cancellationToken);
                if (session == null || session.User == null)
                {
                    logger.Log(LogLevel.Error, "locator", "An user tried to locate a service without being authenticated.", new { session });
                    throw new ClientException("locator.notAuthenticated");

                }

                return session;
            }, RetryPolicies.IncrementalDelay(2, TimeSpan.FromMilliseconds(500)), cancellationToken, ex => ex is ClientException, peer);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> GetSceneConnectionToken(string serviceType, string serviceName, RequestContext<IScenePeerClient> ctx)
        {

            var session = await TryGetSession(ctx.RemotePeer, ctx.CancellationToken);

            try
            {
                var token = await _locator.GetSceneConnectionToken(serviceType, serviceName, session);

                return token;
            }
            catch (InvalidOperationException ex) when (ex.InnerException is HttpRequestException hre)
            {
                if (hre.StatusCode == HttpStatusCode.NotFound)
                {

                    throw new ClientException("sceneNotFound");
                }
                else
                {
                    throw;
                }
            }

        }


    }
}
