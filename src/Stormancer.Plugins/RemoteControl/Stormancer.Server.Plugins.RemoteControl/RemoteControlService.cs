using Lucene.Net.Search;
using Lucene.Net.Store;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Queries;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    internal class RemoteControlService
    {
        private readonly ISceneHost sceneHost;
        private readonly AgentRepository agents;

        public RemoteControlService(ISceneHost sceneHost, AgentRepository agents)
        {
            this.sceneHost = sceneHost;
            this.agents = agents;
        }
        internal void ConnectAgent(Session session)
        {
            if(session.User == null)
            {
                return;
            }
            var agent = new Agent() { SessionId = SessionId.From(session.SessionId), Name = session.platformId.PlatformUserId, Metadata = (JObject?)session.User.UserData["agentMetadata"]?? new JObject() };

            agents.AddOrUpdateAgent(agent);
        }

        internal void DisconnectAgent(string sessionId)
        {
            agents.RemoveAgent(SessionId.From(sessionId));
        }

    }

    public class AgentCommandOutputEntry
    {
        public SessionId SessionId { get; set; }
        public string Result { get; set; }
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



        public SearchResult<Agent> Filter(JObject query, uint size = 20, uint skip = 0)
        {
            var result = new SearchResult<Agent>();


            using var reader = _index.Writer.GetReader(true);
            var searcher = new IndexSearcher(reader);

            var filter = _filtersEngine.Parse(query);

            var docs = searcher.Search(new ConstantScoreQuery(filter.ToLuceneQuery()), (int)(size+skip));
            result.Hits = docs.ScoreDocs.Skip((int)skip).Select(hit => searcher.Doc(hit.Doc)?.Get("_id"))
                .Where(id => id is not null)
                .Select(id =>
            {
                Debug.Assert(id != null);
                var sessionId = SessionId.From(id);
                var agent = _agents.TryGetValue(sessionId, out var a) ? a : default(Agent);
                return (id,agent);
            })
            .Select(tuple=> new Document<Agent> { Id = tuple.id, Source = tuple.agent });
     
            return result;


        }

    }
}
