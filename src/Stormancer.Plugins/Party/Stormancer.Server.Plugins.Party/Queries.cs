// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Stormancer.Server.Plugins.Queries;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Provides method to search for parties.
    /// </summary>
    public class PartySearchService
    {
        private readonly SearchEngine search;

        /// <summary>
        /// Creates a new instance of the service.
        /// </summary>
        /// <param name="search"></param>
        public PartySearchService(SearchEngine search)
        {
            this.search = search;
        }

        /// <summary>
        /// Search for parties.
        /// </summary>
        /// <param name="query">Query object</param>
        /// <param name="skip"></param>
        /// <param name="size"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<SearchResult<JObject>> SearchParties(JObject query, uint skip, uint size, CancellationToken cancellationToken = default)
        {
            return search.QueryAsync<JObject>(PartyLuceneDocumentStore.PARTY_LUCENE_INDEX, query, skip, size, cancellationToken);
        }


    }


    internal class PartyLuceneDocumentStore : ILuceneDocumentStore
    {
        public const string PARTY_LUCENE_INDEX = "stormancer.party";

        private readonly ILucene lucene;
        private Dictionary<string, (JObject, string)> _data = new Dictionary<string, (JObject, string)>();
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
                        yield return new Document<JObject>(id, JObject.FromObject(new { customData = doc.Item2, indexedData = doc.Item1 }));
                    }
                    else
                    {
                        yield return new Document<JObject>(id, default);
                    }
                }
            }
        }

        public bool Handles(string type)
        {
            return type == PARTY_LUCENE_INDEX;
        }

        public void Initialize()
        {
            lucene.TryCreateIndex(PARTY_LUCENE_INDEX, DefaultMapper.JsonMapper);
        }

        public void UpdateDocument(string id, JObject? document, string customData)
        {
            if (document != null)
            {
                lock (syncRoot)
                {

                    if (!_data.TryGetValue(id, out var current) || !JToken.DeepEquals(document, current.Item1))
                    {

                        lucene.IndexDocument(PARTY_LUCENE_INDEX, id, document);
                    }
                    _data[id] = (document, customData);
                }
            }
            else
            {
                DeleteDocument(id);

            }
        }

        public void DeleteDocument(string id)
        {
            var mustRemove = false;
            lock (syncRoot)
            {
                mustRemove = _data.Remove(id);
                
            }
            if(mustRemove)
            {
                lucene.DeleteDocument(PARTY_LUCENE_INDEX, id);
            }
          

        }
    }
}
