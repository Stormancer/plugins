using Stormancer.Server.Plugins.API;
using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// Provides network services for the analytics system.
    /// </summary>
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analytics;
        private readonly ILogger _logger;
        private readonly IEnumerable<IAnalyticsEventHandler> _eventHandlers;
        private const string TypeRegex = "^[A-Za-z0-9_-]+$";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="analytics"></param>
        /// <param name="logger"></param>
        /// <param name="eventHandlers"></param>
        public AnalyticsController(IAnalyticsService analytics, ILogger logger, IEnumerable<IAnalyticsEventHandler> eventHandlers)
        {
            _logger = logger;
            _eventHandlers = eventHandlers;
            _analytics = analytics;
        }

        /// <summary>
        /// Sends a list of analytics events in the analytics system.
        /// </summary>
        /// <param name="docs"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.FireForget)]
        public async Task Push(IEnumerable<EventDto> docs)
        {

            await _eventHandlers.RunEventHandler(h => h.OnClientAnalyticsReceived(docs), ex => _logger.Log(LogLevel.Error, "analytics", $"An error occurred while running '{nameof(IAnalyticsEventHandler.OnClientAnalyticsReceived)}'.", ex));
            foreach (var doc in docs)
            {

                if (!string.IsNullOrEmpty(doc.Type) && System.Text.RegularExpressions.Regex.IsMatch(doc.Type, TypeRegex))
                {
                    // Add some metadata for kibana
                    try
                    {
                        var content = JsonElement.Parse(doc.Content);//Try parse document  
                        AnalyticsDocument document = new AnalyticsDocument { Category = doc.Category, Type = doc.Type, Content = content, CreationDate = doc.CreatedOn == 0 ? DateTime.UtcNow : DateTime.UnixEpoch.AddMilliseconds(doc.CreatedOn) };
                        _analytics.Push(document);
                    }
                    catch (Exception)
                    {
                        _logger.Log(LogLevel.Error, "analytics", $"Invalid analytics json received", doc.Content);
                    }
                }
            }
        

        }
    }
}
