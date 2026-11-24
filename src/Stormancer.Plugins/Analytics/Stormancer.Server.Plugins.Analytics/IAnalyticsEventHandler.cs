using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// Enables plugins to customize analytics events.
    /// </summary>
    public interface IAnalyticsEventHandler
    {
        /// <summary>
        /// Event triggered when a batch of analytics events were received from the client.
        /// </summary>
        /// <param name="docs"></param>
        /// <returns></returns>
        Task OnClientAnalyticsReceived(IEnumerable<EventDto> docs)
        {
            return Task.CompletedTask;
        }
    }
}
