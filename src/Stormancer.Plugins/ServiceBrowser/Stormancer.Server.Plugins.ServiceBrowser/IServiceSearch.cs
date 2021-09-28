using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.ServiceBrowser
{
    public class SearchRequest
    {
        public string Type { get; set; }
        public JObject Filter { get; set; }
        public uint Size { get; set; }
    }

    /// <summary>
    /// Provides APIs to search and reserve connection slots to services.
    /// </summary>
    public class ServiceSearchEngine
    {
        private readonly IHost host;
        private readonly ISerializer serializer;
        private readonly Func<IEnumerable<IServiceSearchProvider>> providers;

        public ServiceSearchEngine(IHost host,ISerializer serializer, Func<IEnumerable<IServiceSearchProvider>> providers)
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
        /// <param name="size"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<Document<T>> QueryAsync<T>(string type, JObject filter,uint size,[EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var searchRqArgs = new SearchRequest { Type = type, Filter = filter, Size = size };
            using var request =await host.StartAppFunctionRequest("ServiceSearch.Query", cancellationToken);

            await serializer.SerializeAsync(searchRqArgs, request.Input, cancellationToken);
           
            request.Input.Complete();

            await foreach(var result in request.Results)
            {
                if (result.IsSuccess)
                {
                    var docs = await serializer.DeserializeAsync<IEnumerable<Document<T>>>(result.Output, cancellationToken);
                    
                    foreach(var doc in docs)
                    {
                        yield return doc;
                    }
                }
                result.Output.Complete();
            }
            
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
            foreach(var provider in providers)
            {
                if(provider.Handles(rq.Type))
                {
                    await serializer.SerializeAsync(provider.Filter(rq.Filter,rq.Size),ctx.Output,CancellationToken.None);
                }
            }

            ctx.Output.Complete();
        }
        
    }

    public class Document<T>
    {
        public string Id { get; set; }

        public T Source { get; set; }
    }

}
