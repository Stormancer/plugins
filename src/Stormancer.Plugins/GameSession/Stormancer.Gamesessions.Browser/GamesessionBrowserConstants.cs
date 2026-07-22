using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Gamesessions.Browser
{
    /// <summary>
    /// Constants for the game session browser plugin.
    /// </summary>
    static public class GamesessionBrowserConstants
    {
        /// <summary>
        /// Metadata signaling the presence of the search API.
        /// </summary>
        public const string METADATA_KEY = "stormancer.gamesessions.search";

        /// <summary>
        /// Id of the scene hosting the search service.
        /// </summary>
        public const string SCENE_ID = "gamesession-search";

        /// <summary>
        /// Type of the scene hosting the search service.
        /// </summary>
        public const string SCENE_TYPE = "gamesessionsManager";
    }
}
