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

namespace Stormancer.Server.Plugins.Gameye
{
    public class StartGameServerParameters
    {
        public string Id { get; set; } = default!;
        public string Location { get; set; } = default!;
        public string Image { get; set; } = default!;
        public Dictionary<string, string>? Env { get; set; }

        public IEnumerable<string>? Args { get; set; }

        public bool Restart { get; set; }

        public Dictionary<string, string>? Labels { get; set; }
    }

    public class GameyeServerError
    {
        public HttpStatusCode HttpError { get; set; }
        public Exception? Exception { get; set; }
    }


    public class StartGameServerResult
    {
        public class Port
        {
            public string Type { get; set; } = default!;
            public int Container { get; set; }
            public int Host { get; set; }
        }

        public string Host { get; set; } = default!;
        public IEnumerable<Port> Ports { get; set; } = default!;
    }

    public class ListGameServersResult
    {
        public class GameServerDocument
        {
            public string Id { get; set; }
            public string Image { get; set; }
            public string Location { get; set; }
            public string Host { get; set; }
            public long Created { get; set; }
            public Dictionary<string, string> Labels { get; set; }

        }
        public IEnumerable<GameServerDocument> Sessions { get; set; } = Enumerable.Empty<GameServerDocument>();
    }

    public class GameyeConfigurationSection
    {
        public const string PATH = "gameye";

        /// <summary>
        /// Path in the secret store for the Gameye  authentication key.
        /// </summary>
        public string? AuthenticationKeyPath { get; set; }
        public int AuthenticationKeyRefreshTimeSeconds { get; set; } = 5;
    }
    internal class GameyeClient
    {
        private readonly IConfiguration _configuration;
        private readonly ISecretsStore _secretsStore;

        private MemoryCache<int, string> _tokenCache = new MemoryCache<int, string>();
        public GameyeClient(IConfiguration configuration, ISecretsStore secretsStore)
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
        private async Task<AuthenticationHeaderValue> GetAuthorizationHeaderAsync()
        {

            var token = await _tokenCache.Get(0, async (_) =>
            {
                var section = _configuration.GetValue<GameyeConfigurationSection>(GameyeConfigurationSection.PATH, new GameyeConfigurationSection());
                if (section.AuthenticationKeyPath != null)
                {
                    return (await GetAuthenticationTokenAsync(section.AuthenticationKeyPath), TimeSpan.FromMinutes(section.AuthenticationKeyRefreshTimeSeconds));
                }
                else
                {
                    return (null, TimeSpan.FromSeconds(1));
                }

            });
           
            return new AuthenticationHeaderValue("Bearer", token);


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
        public async Task<Result<StartGameServerResult, GameyeServerError>> StartGameServerAsync(StartGameServerParameters args, CancellationToken cancellationToken)
        {
            
            using var request = new HttpRequestMessage(HttpMethod.Post, "/session");
            request.Headers.Authorization = await GetAuthorizationHeaderAsync();
            request.Content = JsonContent.Create(args);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var result = await response.Content.ReadFromJsonAsync<StartGameServerResult>((JsonSerializerOptions?)null, cancellationToken);
                if (result == null)
                {
                    return Result<StartGameServerResult, GameyeServerError>.Failed(new GameyeServerError { Exception = new InvalidOperationException("invalidResponse") });
                }
                return Result<StartGameServerResult, GameyeServerError>.Succeeded(result);

            }
            else
            {
                return Result<StartGameServerResult, GameyeServerError>.Failed(new GameyeServerError
                {
                    HttpError = response.StatusCode
                });
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// https://docs.gameye.com/lists-a-collection-of-active-sessions
        /// </remarks>
        /// <returns></returns>
        public async Task<Result<ListGameServersResult, GameyeServerError>> ListGameServersAsync(string? filter, CancellationToken cancellationToken)
        {
           
            var uriBuilder = new UriBuilder("/session");

            if (filter != null)
            {
                uriBuilder.Query = $"?filter={filter}";
            }
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Authorization = await GetAuthorizationHeaderAsync();
            using var response = await _httpClient.SendAsync(request, cancellationToken);


            if (response.StatusCode == HttpStatusCode.Created)
            {
                var result = await response.Content.ReadFromJsonAsync<ListGameServersResult>((JsonSerializerOptions?)null, cancellationToken);
                if (result == null)
                {
                    return Result<ListGameServersResult, GameyeServerError>.Failed(new GameyeServerError { Exception = new InvalidOperationException("invalidResponse") });
                }
                return Result<ListGameServersResult, GameyeServerError>.Succeeded(result);

            }
            else
            {
                return Result<ListGameServersResult, GameyeServerError>.Failed(new GameyeServerError
                {
                    HttpError = response.StatusCode
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
        public async Task<Result<GameyeServerError>> StopGameServerAsync(string id, CancellationToken cancellationToken)
        {
           
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/session/{id}");
            request.Headers.Authorization = await GetAuthorizationHeaderAsync();
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return Result<GameyeServerError>.Succeeded();
            }
            else
            {
                return Result<GameyeServerError>.Failed(new GameyeServerError
                {
                    HttpError = response.StatusCode
                });

            }
        }
    }

    //internal class GameyeHttpClientHandler : DelegatingHandler
    //{
    //    private readonly string? _parameters;

    //    public GameyeHttpClientHandler(string? parameters)
    //    {
    //        _parameters = parameters;
    //    }
    //    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    //    {
    //        if (_parameters != null)
    //        {
    //            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _parameters);
    //        }

    //        return base.SendAsync(request, cancellationToken);
    //    }
    //}
}
