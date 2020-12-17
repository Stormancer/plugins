using Microsoft.Extensions.Http;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.HttpClientFactory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Management
{
    class ManagementHttpClientFactoryEventHandler : IHttpClientFactoryEventHandler
    {
        private readonly IEnvironment env;

        private class StormancerManagementAuthDelegatingHandler : DelegatingHandler
        {
            private readonly IEnvironment env;

            public StormancerManagementAuthDelegatingHandler(IEnvironment env)
            {
                this.env = env;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var creds = await env.GetAdminApiCredentials();
              
                foreach (var header in creds.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                return await base.SendAsync(request, cancellationToken);
            }
        }

        public ManagementHttpClientFactoryEventHandler(IEnvironment env)
        {
            this.env = env;
        }

        public HttpClientFactoryOptions? GetOptions(string name)
        {
            if (name == "stormancer.management")
            {

                var options = new HttpClientFactoryOptions();
                options.HttpMessageHandlerBuilderActions.Add(builder => {

                    builder.AdditionalHandlers.Add(new StormancerManagementAuthDelegatingHandler(env));
                });
            }
            return null;
        }
    }
}
