using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.RemoteControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Plugins.RemoteControl
{
    /// <summary>
    /// A document returned by a query.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Document<T>
    {
        /// <summary>
        /// Gets or sets the id of the document.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the content of the document.
        /// </summary>
        public T? Source { get; set; }
    }

    /// <summary>
    /// Results of a search
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SearchResult<T>
    {
        /// <summary>
        /// Total of documents matching the query.
        /// </summary>
        public uint Total { get; set; }

        /// <summary>
        /// Documents in the response.
        /// </summary>
        public IEnumerable<Document<T>> Hits { get; set; } = default!;
    }

    public class AgentCommandOutputEntry
    {
        public SessionId SessionId { get; set; }
        public string AgentName { get; set; }

        public string Type { get; set; }
        public JObject Result { get; set; }
    }

    public class RemoteControlClientApi : ClientAPI<RemoteControlAgentApi>
    {
        public RemoteControlClientApi(UserApi authenticationService) : base(authenticationService)
        {
        }

        public async IAsyncEnumerable<AgentCommandOutputEntry> RunCommandAsync(string command, IEnumerable<SessionId> sessionIds, CancellationToken cancellationToken)
        {
            var service = await GetService<RemoteControlClientService>("stormancer.remoteControl");

            await foreach(var entry in service.RunCommandAsync(command, sessionIds, cancellationToken))
            {
                yield return entry;
            }
        }


        public async Task<SearchResult<Agent>> SearchAgentsAsync(string query, uint size, uint skip, CancellationToken cancellationToken)
        {
            var service = await GetService<RemoteControlClientService>("stormancer.remoteControl");

            return await service.SearchAgentsAsync(query, size, skip, cancellationToken);
        }

    }

    internal class RemoteControlClientService
    {
        private readonly Scene scene;
        private readonly ISerializer serializer;

        public RemoteControlClientService(IScene scene, ISerializer serializer)
        {
            this.scene = (Scene)scene;
            this.serializer = serializer;
        }


        public async IAsyncEnumerable<AgentCommandOutputEntry> RunCommandAsync(string command, IEnumerable<SessionId> sessionIds,[EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var results = scene.Rpc("RemoteControl.RunCommand", s => {
                serializer.Serialize(command, s);
                serializer.Serialize(sessionIds, s);
                }).ToAsyncEnumerable().WithCancellation(cancellationToken);


            await foreach(var packet in results)
            {
                var outputs =  serializer.Deserialize<IEnumerable<AgentCommandOutputEntry>>(packet.Stream);
                foreach(var output in outputs)
                {
                    yield return output;
                }
            }
        }


        public Task<SearchResult<Agent>> SearchAgentsAsync(string query, uint size, uint skip, CancellationToken cancellationToken)
        {
            return scene.RpcTask<SearchResult<Agent>>("RemoteControl.SearchAgents", s =>
            {
                serializer.Serialize(query, s);
                serializer.Serialize(size, s);
                serializer.Serialize(skip, s);
            },cancellationToken);
        }
    }
}
