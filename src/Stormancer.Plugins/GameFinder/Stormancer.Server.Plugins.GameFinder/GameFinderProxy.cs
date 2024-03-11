using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Provides access to game finder capabilities.
    /// </summary>
    partial class GameFinderProxy
    {

      

    }

    /// <summary>
    /// Metrics sent by a game finder.
    /// </summary>
    public record GameFinderMetrics
    {
        /// <summary>
        /// Gets the name of the game finder. 
        /// </summary>
        public string GameFinderName { get; init; } = default!;

        /// <summary>
        /// Gets the gamefinder metrics.
        /// </summary>
        public Dictionary<string, int> Metrics { get; init; } = default!;
    }
}
