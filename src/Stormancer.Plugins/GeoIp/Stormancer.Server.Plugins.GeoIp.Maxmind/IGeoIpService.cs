using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GeoIp
{
    /// <summary>
    /// Provides GeoIp related methods.
    /// </summary>
    public interface IGeoIpService
    {
        /// <summary>
        /// Gets a code representing the country the provided IP is located in.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<GeoIpCountryResult> GetCountryAsync(string ip, CancellationToken cancellationToken = default);
    }

    public class GeoIpCountryResult
    {
        public GeoIpCountryResult(string? continent, string? country)
        {
            Continent = continent;
            Country = country;
        }

        public string? Continent { get; }
        public string ? Country { get;}
    }
}