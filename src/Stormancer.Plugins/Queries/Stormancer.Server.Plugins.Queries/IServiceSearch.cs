using MessagePack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Queries
{
    /// <summary>
    /// Search request
    /// </summary>
    [MessagePackObject]
    public class SearchRequest
    {
        /// <summary>
        /// Type of documents to search for.
        /// </summary>
        [Key(0)]
        public string Type { get; set; } = default!;

        /// <summary>
        /// Query filter.
        /// </summary>
        /// <remarks>
        /// Null is empty.(match all)
        /// </remarks>
        [Key(1)]
        public JObject? Filter { get; set; }

        /// <summary>
        /// Number of items to return.
        /// </summary>
        [Key(2)]
        public uint Size { get; set; } = 10;
    }

    /// <summary>
    /// Results of a search
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [MessagePackObject]
    public class SearchResult<T>
    {
        /// <summary>
        /// Total of documents matching the query.
        /// </summary>
        [Key(0)]
        public uint Total { get; set; }

        /// <summary>
        /// Documents in the response.
        /// </summary>
        [Key(1)]
        public IEnumerable<Document<T>> Hits { get; set; } = default!;
    }
    /// <summary>
    /// Provides APIs to search and reserve connection slots to services.
    /// </summary>
    public class SearchEngine
    {
        private readonly IHost host;
        private readonly IClusterSerializer serializer;
        private readonly Func<IEnumerable<IServiceSearchProvider>> providers;


        /// <summary>
        /// Creates a new <see cref="SearchEngine"/> object.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="serializer"></param>
        /// <param name="providers"></param>
        public SearchEngine(IHost host, IClusterSerializer serializer, Func<IEnumerable<IServiceSearchProvider>> providers)
        {
            this.host = host;
            this.serializer = serializer;
            this.providers = providers;
        }

        /// <summary>
        /// Searches for active services of the provided type in the application.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="filter"></param>
        /// <param name="skip"></param>
        /// <param name="size"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<SearchResult<T>> QueryAsync<T>(string type, JObject filter, uint skip, uint size, CancellationToken cancellationToken)
        {
            var searchRqArgs = new SearchRequest { Type = type, Filter = filter, Size = size + skip };
            using var request = await host.CreateAppFunctionRequest("ServiceSearch.Query", cancellationToken);

            serializer.Serialize(request.Input, searchRqArgs);

            request.Input.Complete();
            request.Send();

            var result = new SearchResult<T>();
            var hits = new List<Document<T>>();
            result.Hits = hits;
            var current = 0;
            await foreach (var appFuncResult in request.Results)
            {
                if (appFuncResult.IsSuccess)
                {
                    var partialResult = await serializer.DeserializeAsync<SearchResult<T>>(appFuncResult.Output, cancellationToken);

                    foreach (var doc in partialResult.Hits)
                    {
                        if (current >= skip && current < skip + size)
                        {
                            hits.Add(doc);
                        }
                        current++;

                    }
                    result.Total += partialResult.Total;
                }
                appFuncResult.Output.Complete();
            }

            return result;

        }

        internal void Initialize()
        {
            host.RegisterAppFunction("ServiceSearch.Query", OnQuery);
        }

        private async Task OnQuery(IAppFunctionContext ctx)
        {
            var providers = this.providers();
            var rq = await serializer.DeserializeAsync<SearchRequest>(ctx.Input, CancellationToken.None);

            ctx.Input.Complete();
            foreach (var provider in providers)
            {
                if (provider.Handles(rq.Type))
                {
                    serializer.Serialize(ctx.Output, provider.Filter(rq.Type, rq.Filter ?? new JObject(), rq.Size));
                }
            }

            ctx.Output.Complete();
        }

    }

    /// <summary>
    /// A document returned by a query.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [MessagePackObject]
    public class Document<T>
    {
        /// <summary>
        /// Creates a new <see cref="Document{T}"/>.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="source"></param>
        public Document(string  id, T? source)
        {
            Id = id;
            Source = source;
        }
        /// <summary>
        /// Gets or sets the id of the document.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the content of the document.
        /// </summary>
        public T? Source { get; set; }

        /// <summary>
        /// Gets or sets the version of the document.
        /// </summary>
        public required uint Version { get; init; }
    }



}
