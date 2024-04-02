using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Model;
using Microsoft.Extensions.Options;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GeoIp.Maxmind
{
    public class MaxMindWebClientOptions
    {
        public const string CONFIG_SECTION = "geoip.maxmind";

        public int AccountId { get; set; }
        public string? LicenseKeyPath { get; set; }

        /// <summary>
        /// Enables or disables Geo IP if accountId and licenseKeyPath are set.
        /// </summary>
        /// <remarks>
        /// Defaults to true.
        /// </remarks>
        public bool Enabled { get; set; } = true;
    }
    internal class MaxMindGeoIpService : IGeoIpService
    {
        private readonly IConfiguration _config;
        private readonly ISecretsStore _secretsStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<MaxMindWebClientOptions> _options;

        public MaxMindGeoIpService(IConfiguration config, ISecretsStore secretsStore , IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _secretsStore = secretsStore;
            _httpClientFactory = httpClientFactory;
            _options = _config.GetOptions<MaxMindWebClientOptions>(MaxMindWebClientOptions.CONFIG_SECTION);
        }

        private class LazyMaxMindOptions : IOptions<MaxMind.GeoIP2.WebServiceClientOptions>
        {
            public LazyMaxMindOptions(WebServiceClientOptions value)
            {
                Value = value;
            }
            public WebServiceClientOptions Value { get; }
        }

        MemoryCache<string,string> _cache = new MemoryCache<string,string>();

        public bool IsGeoIpEnabled => _options.Value.Enabled;

        private Task<string?> GetLicenseKey(string path)
        {
            return _cache.Get("licenseKey",async _=> {
                var key = await _secretsStore.GetSecret(path);
                if(key.Value == null)
                {
                    throw new InvalidOperationException($"MaxMind License key not found in '{path}'");
                }
                return (Encoding.UTF8.GetString(key.Value), TimeSpan.FromMinutes(5));
            });
        }
        public async Task<GeoIpCountryResult?> GetCountryAsync(string ip, CancellationToken cancellationToken = default)
        {
            
            var configSection = _options.Value;

            if (configSection.LicenseKeyPath == null)
            {
                return null;
            }
            var licenseKey = await GetLicenseKey(configSection.LicenseKeyPath);
            if(licenseKey == null)
            {
                throw new InvalidOperationException($"MaxMind License key not found.");
            }

            var options = new WebServiceClientOptions { AccountId = configSection.AccountId, LicenseKey = licenseKey };
            var client = new WebServiceClient(_httpClientFactory.CreateClient("geoip.MaxMind"), new LazyMaxMindOptions(options));

            var response = await client.CountryAsync(ip);

            return new GeoIpCountryResult(response.Continent.Code, response.Country.IsoCode);
        }
    }

}