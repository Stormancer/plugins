using Lucene.Net.Search;
using Lucene.Net.Store;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Queries;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    public interface IRemoteControlService
    {
        IAsyncEnumerable<AgentCommandOutputEntry> RunCommandAsync(string command, IEnumerable<SessionId> agents, CancellationToken cancellationToken);

        SearchResult<Agent> SearchAgents(JObject query, uint size = 20, uint skip = 0);


        Dictionary<SessionId, Agent?> GetAgents(IEnumerable<SessionId> ids);
    }
    internal class RemoteControlService : IRemoteControlService
    {
        private readonly ISceneHost sceneHost;
        private readonly AgentRepository _agents;
        private readonly ISerializer serializer;

        public RemoteControlService(ISceneHost sceneHost, AgentRepository agents, ISerializer serializer)
        {
            this.sceneHost = sceneHost;
            this._agents = agents;
            this.serializer = serializer;
        }

        public Dictionary<SessionId, Agent?> GetAgents(IEnumerable<SessionId> ids) => _agents.GetAgents(ids);


        public async IAsyncEnumerable<AgentCommandOutputEntry> RunCommandAsync(string command, IEnumerable<SessionId> agentIds, CancellationToken cancellationToken)
        {
            var agents = _agents.GetAgents(agentIds);

            foreach (var agent in agents.Values.WhereNotNull())
            {
                if (agent.RunningCommand != null)
                {
                    throw new InvalidOperationException($"Agent {agent.Name} is currently running command '{agent.RunningCommand.Command}'.");
                }
            }


            async IAsyncEnumerable<AgentCommandOutputEntry> RunCommand(Agent agent, string command, CancellationToken cancellationToken)
            {
                var rpc = sceneHost.DependencyResolver.Resolve<RpcService>();
                var peer = sceneHost.RemotePeers.FirstOrDefault(p => p.SessionId == agent.SessionId.ToString());
                if (peer != null)
                {
                    await foreach (var packet in rpc.Rpc("runCommand", peer, s => serializer.Serialize(command, s), PacketPriority.MEDIUM_PRIORITY, cancellationToken).ToAsyncEnumerable())
                    {
                        using (packet)
                        {
                            var dto = packet.ReadObject<AgentCommandOutputEntryDto>();
                            JObject? content = null;
                            string? error = null;
                            try
                            {
                                content = JObject.Parse(dto.ResultJson);
                            }
                            catch(Exception ex)
                            {
                                error = ex.ToString();
                            }

                            if(content is not null)
                            {
                                yield return new AgentCommandOutputEntry
                                {
                                    SessionId = agent.SessionId,
                                    AgentName = agent.Name,
                                    Result = JObject.Parse(dto.ResultJson),
                                    Type = dto.Type
                                };
                            }
                            else
                            {
                                yield return new AgentCommandOutputEntry
                                {
                                    SessionId = agent.SessionId,
                                    AgentName = agent.Name,
                                    Result = JObject.FromObject(new { error = error, json =  dto.ResultJson }),
                                    Type = "error"
                                };
                            }
                        }
                    }
                }
            }


            await foreach (var entry in agents.Values.WhereNotNull().Select(a => RunCommand(a, command, cancellationToken)).SelectManyInterlaced(cancellationToken))
            {
                yield return entry;
            }
        }

        public SearchResult<Agent> SearchAgents(JObject query, uint size = 20, uint skip = 0)
        {
            return _agents.Filter(query, size, skip);
        }

        internal void ConnectAgent(Session session)
        {
            if (session.User == null)
            {
                return;
            }
            var agent = new Agent(SessionId.From(session.SessionId), session.platformId.PlatformUserId, (JObject?)session.User.UserData["agentMetadata"] ?? new JObject());

            _agents.AddOrUpdateAgent(agent);
        }


        internal void DisconnectAgent(string sessionId)
        {
            _agents.RemoveAgent(SessionId.From(sessionId));
        }

    }

    public class AgentCommandOutputEntryDto
    {
        public string Type { get; set; }
        public string ResultJson { get; set; }
    }

    public class AgentCommandOutputEntry
    {
        public SessionId SessionId { get; set; }
        public string AgentName { get; set; }

        public string Type { get; set; }
        public JObject Result { get; set; }
    }


    internal class AgentRepository
    {
        private IndexState _index;

        private Dictionary<SessionId, Agent> _agents = new Dictionary<SessionId, Agent>();

        private object _syncRoot = new object();
        public AgentRepository()
        {
            _index = new IndexState(new RAMDirectory(), DefaultMapper.JsonMapper);
        }

        private Filters _filtersEngine = new Filters(new IFilterExpressionFactory[] { new CommonFiltersExpressionFactory() });

        public void AddOrUpdateAgent(Agent agent)
        {
            var sessionId = agent.SessionId;
            var id = sessionId.ToString();
            _index.Writer.UpdateDocument(new Lucene.Net.Index.Term("_id", id), _index.Mapper(id, agent.Metadata));
            _index.Writer.Flush(false, false);
            lock (_syncRoot)
            {
                _agents[sessionId] = agent;
            }
        }
        public void RemoveAgent(SessionId sessionId)
        {
            bool success = false;
            lock (_syncRoot)
            {
                success = _agents.Remove(sessionId, out _);

            }

            if (success)
            {
                _index.Writer.DeleteDocuments(new Lucene.Net.Index.Term("_id", sessionId.ToString()));
                _index.Writer.Flush(false, false);
            }
        }

        public Dictionary<SessionId, Agent?> GetAgents(IEnumerable<SessionId> sessionIds)
        {
            lock (_syncRoot)
            {
                var dictionary = new Dictionary<SessionId, Agent?>();

                foreach (var sessionId in sessionIds)
                {
                    dictionary[sessionId] = _agents.TryGetValue(sessionId, out var agent) ? agent : null;
                }
                return dictionary;

            }
        }

        public SearchResult<Agent> Filter(JObject query, uint size = 20, uint skip = 0)
        {
            var result = new SearchResult<Agent>();


            using var reader = _index.Writer.GetReader(true);
            var searcher = new IndexSearcher(reader);

            var filter = _filtersEngine.Parse(query);

            var docs = searcher.Search(new ConstantScoreQuery(filter.ToLuceneQuery()), (int)(size + skip));
            result.Hits = docs.ScoreDocs.Skip((int)skip).Select(hit => searcher.Doc(hit.Doc)?.Get("_id"))
                .Where(id => id is not null)
                .Select(id =>
            {
                Debug.Assert(id != null);
                var sessionId = SessionId.From(id);
                var agent = _agents.TryGetValue(sessionId, out var a) ? a : default(Agent);
                return (id, agent);
            })
            .Select(tuple => new Document<Agent> { Id = tuple.id, Source = tuple.agent });

            return result;


        }

    }
}
