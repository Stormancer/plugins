using Server.Plugins.API;
using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics
{
    class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analytics;
        private readonly ILogger _logger;
        private const string TypeRegex = "^[A-Za-z0-9_-]+$";

        public AnalyticsController(IAnalyticsService analytics, ILogger logger)
        {
            _logger = logger;
            _analytics = analytics;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task Push(IEnumerable<DocumentDto> docs)
        {


            foreach (var doc in docs)
            {
                if (!string.IsNullOrEmpty(doc.Type) && System.Text.RegularExpressions.Regex.IsMatch(doc.Type, TypeRegex))
                {
                    // Add some meta data for kibana
                    try
                    {
                        Newtonsoft.Json.Linq.JObject content = Newtonsoft.Json.Linq.JObject.Parse(doc.Content);//Try parse document  
                        AnalyticsDocument document = new AnalyticsDocument { Category = doc.Category, Type = doc.Type, Content = content, CreationDate = DateTime.UtcNow };
                        _analytics.Push(document);
                        //_logger.Log(LogLevel.Info, "analytics", $"Successfully pushed analytics document", doc.Content);
                    }
                    catch(Exception)
                    {
                        _logger.Log(LogLevel.Error, "analytics", $"Invalid analytics json received", doc.Content);
                    }
                }
            }
            return Task.CompletedTask;

        }
    }
}
