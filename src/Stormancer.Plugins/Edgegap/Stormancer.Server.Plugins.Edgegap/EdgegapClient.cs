using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Edgegap
{
    public class EnvironmentVariable
    {
        public string key { get; set; }
        public string value { get; set; }
        public bool is_hidden { get; set; }
    }
    public class Filter
    {
        public string field { get; set; }
        public IEnumerable<string> values { get; set; }
        public string filter_type { get; set; }
    }

    public class StartGameServerParameters
    {
        public string app_name { get; set; } = default!;
        public string version_name { get; set; } = default!;
        public IEnumerable<EnvironmentVariable> env_vars { get; set; } = Enumerable.Empty<EnvironmentVariable>();

        public IEnumerable<Filter> filters { get; set; } = Enumerable.Empty<Filter>();
        public IEnumerable<string>? tags { get; set; }
    }

    public class EdgegapServerError
    {
        public HttpStatusCode HttpError { get; set; }
        public Exception? Exception { get; set; }
    }


    public class StartGameServerResult
    {
        public string request_id { get; set; }
    }

    public class StartGameServerError
    {
        public string message { get; set; }
    }

    public class EdgegapConfigurationSection
    {
        public const string PATH = "edgegap";

        /// <summary>
        /// Path in the secret store for the Gameye  authentication key.
        /// </summary>
        public string? AuthenticationKeyPath { get; set; }
        public int AuthenticationKeyRefreshTimeSeconds { get; set; } = 5;
    }
    internal class EdgegapClient
    {
        private readonly IConfiguration _configuration;
        private readonly ISecretsStore _secretsStore;

        private MemoryCache<int, string> _tokenCache = new MemoryCache<int, string>();
        public EdgegapClient(IConfiguration configuration, ISecretsStore secretsStore)
        {
            _configuration = configuration;
            _secretsStore = secretsStore;


        }

        private async Task<string?> GetAuthenticationTokenAsync(string authenticationKeyPath)
        {
            var secret = await _secretsStore.GetSecret(authenticationKeyPath);

            if (secret.Value != null)
            {
                return Encoding.UTF8.GetString(secret.Value);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Use a long lived HttpClient using the SocketsHttpHandler to share a connection to the HTTP server. 
        /// </summary>
        private static HttpClient _httpClient = CreateClient();
        private static HttpClient CreateClient()
        {

            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15) // Recreate every 15 minutes to refresh DNS.
            };

            var client = new HttpClient(handler);
            client.BaseAddress = new Uri("https://api.gameye.io");
            return client;
        }

        private async Task<string?> GetAuthorizationHeaderAsync()
        {

            return await _tokenCache.Get(0, async (_) =>
            {
                var section = _configuration.GetValue(EdgegapConfigurationSection.PATH, new EdgegapConfigurationSection());
                if (section.AuthenticationKeyPath != null)
                {
                    return (await GetAuthenticationTokenAsync(section.AuthenticationKeyPath), TimeSpan.FromMinutes(section.AuthenticationKeyRefreshTimeSeconds));
                }
                else
                {
                    return (null, TimeSpan.FromSeconds(1));
                }

            });




        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// https://docs.gameye.com/session
        /// </remarks>
        /// <returns></returns>
        public async Task<Result<StartGameServerResult, EdgegapServerError>> StartGameServerAsync(StartGameServerParameters args, CancellationToken cancellationToken)
        {

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/deploy");
            var auth = await GetAuthorizationHeaderAsync();
            if (auth != null)
            {
                request.Headers.Add("Authorization", auth);
            }
            request.Content = JsonContent.Create(args);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<StartGameServerResult>((JsonSerializerOptions?)null, cancellationToken);
                if (result == null)
                {
                    return Result<StartGameServerResult, EdgegapServerError>.Failed(new EdgegapServerError { Exception = new InvalidOperationException("invalidResponse") });
                }
                return Result<StartGameServerResult, EdgegapServerError>.Succeeded(result);

            }
            else
            {
                var result = await response.Content.ReadFromJsonAsync<StartGameServerError>((JsonSerializerOptions?)null, cancellationToken);
                return Result<StartGameServerResult, EdgegapServerError>.Failed(new EdgegapServerError
                {
                    HttpError = response.StatusCode,
                    Exception = new Exception(result?.message)
                });
            }

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// https://docs.gameye.com/sessionid
        /// </remarks>
        /// <returns></returns>
        public async Task<Result<EdgegapServerError>> StopGameServerAsync(string requestId, CancellationToken cancellationToken)
        {

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"v1/stop/{requestId}");
            var auth = await GetAuthorizationHeaderAsync();
            if (auth != null)
            {
                request.Headers.Add("Authorization", auth);
            }
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.Gone )
            {
                return Result<EdgegapServerError>.Succeeded();
            }
            else
            {
                var result = await response.Content.ReadFromJsonAsync<StartGameServerError>((JsonSerializerOptions?)null, cancellationToken);
                return Result<EdgegapServerError>.Failed(new EdgegapServerError
                {
                    Exception = new Exception(result?.message),
                    HttpError = response.StatusCode
                }) ;

            }
        }

        internal IAsyncEnumerable<string> QueryLogsAsync(string id, DateTime? since, DateTime? until, uint size, bool follow, CancellationToken cancellationToken)
        {
            return AsyncEnumerable.Empty<string>();
        }
    }
}
