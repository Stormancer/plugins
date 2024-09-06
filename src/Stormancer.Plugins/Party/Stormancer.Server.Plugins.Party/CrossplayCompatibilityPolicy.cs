using Stormancer.Abstractions.Server.GameFinder;
using Stormancer.Server.Plugins.Models;

using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{

    /// <summary>
    /// A policy evaluating if 2 parties are compatible using cross play information.
    /// </summary>
    internal class CrossPlayPartyCompatibilityPolicy : IPartyCompatibilityPolicy
    {

        ///<inheritdoc/>
        public Task<CompatibilityTestResult> AreCompatible(Models.Party party1, Models.Party party2, object context)
        {
            // If crossplay is enabled on a party, platform == "". Cross play parties are NOT compatible with non cross play parties to prevent difficult to understand situations.
            if (party1.Platform != party2.Platform)
            {
                return Task.FromResult(new CompatibilityTestResult(false, "crossplay"));
            }
            else
            {
                return Task.FromResult(new CompatibilityTestResult(true));
            }

        }


    }
}
