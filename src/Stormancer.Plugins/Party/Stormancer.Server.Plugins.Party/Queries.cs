using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Queries;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    internal class PartyLuceneDocumentStore : ILuceneDocumentStore
    {
        public const string PARTY_LUCENE_INDEX = "stormancer.party";

        private readonly ILucene lucene;
        private Dictionary<string, JObject> _data = new Dictionary<string, JObject>();
        private object syncRoot = new object();
        public PartyLuceneDocumentStore(ILucene lucene)
        {
            this.lucene = lucene;
        }
        public IEnumerable<Document<JObject>> GetDocuments(IEnumerable<string> ids)
        {

            foreach (var id in ids)
            {
                lock (syncRoot)
                {
                    if (_data.TryGetValue(id, out var doc))
                    {
                        yield return new Document<JObject> { Id = id, Source = doc };
                    }
                    else
                    {
                        yield return new Document<JObject> { Id = id, Source = null };
                    }
                }
            }
        }

        public bool Handles(string type)
        {
            return type == "stormancer.parties";
        }

        public void Initialize()
        {
            lucene.TryCreateIndex(PARTY_LUCENE_INDEX, DefaultMapper.JsonMapper);
        }
    }
}
