using Elasticsearch.Net;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database
{
    internal class ElasticsearchExceptionHandler : Stormancer.Server.Plugins.API.IApiHandler
    {
        private readonly ILogger _logger;

        public ElasticsearchExceptionHandler(ILogger logger)
        {
            this._logger = logger;
        }

        public async Task RunRpc(ApiCallContext<RequestContext<IScenePeerClient>> ctx, Func<ApiCallContext<RequestContext<IScenePeerClient>>, Task> next)
        {
            try
            {
                await next(ctx);
            }
            catch (UnexpectedElasticsearchClientException ex)
            {
                _logger.Log(LogLevel.Error, "elasticsearch", "An unexpected Elasticsearch error occured.", new
                {
                    ex.StackTrace,
                    ex.Message,
                    ex.AuditTrail,
                    ex.DebugInformation
                });
            }
        }
    }   
    
}
