using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Newtonsoft.Json.Linq;
using Stormancer.Filtering;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Queries
{
    /// <summary>
    /// Represents a lucene index
    /// </summary>
    public class IndexState
    {
        public IndexState(BaseDirectory luceneDirectory, Func<JObject, IEnumerable<IIndexableField>> mapper)
        {
            LuceneDirectory = luceneDirectory;
            customMapper = mapper;

            // Ensures index backward compatibility
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

            //Create an analyzer to process the text
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
            Writer = new IndexWriter(LuceneDirectory, indexConfig);
        }

        public IEnumerable<IIndexableField> Mapper(string id, JObject document)
        {
            yield return new StringField("_id", id, Field.Store.YES);
            foreach (var item in customMapper(document))
            {
                yield return item;
            }
        }
        public BaseDirectory LuceneDirectory { get; }

        private readonly Func<JObject, IEnumerable<IIndexableField>> customMapper;

        public IndexWriter Writer { get; set; }
    }

    /// <summary>
    /// Contract to the Lucene InMemory index.
    /// </summary>
    public interface ILucene
    {
        /// <summary>
        /// Tries creating an index in Lucene.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="mapper"></param>
        /// <returns></returns>
        bool TryCreateIndex(string type, Func<JObject, IEnumerable<IIndexableField>> mapper);

        /// <summary>
        /// Indexes a document in a Lucene index.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="document"></param>
        /// <returns></returns>
        bool IndexDocument(string type, string id, JObject document);

        /// <summary>
        /// Deletes a document from a Lucene index.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool DeleteDocument(string type, string id);
    }

    internal class LuceneSearchProvider : IServiceSearchProvider, ILucene, IDisposable
    {

        private Dictionary<string, IndexState> _indices = new Dictionary<string, IndexState>();
        private Func<IEnumerable<ILuceneDocumentStore>> _documentStores;
        private Filters _filtersEngine = new Filters(new IFilterExpressionFactory[] { new CommonFiltersExpressionFactory() });

        public LuceneSearchProvider(Func<IEnumerable<ILuceneDocumentStore>> documentStores)
        {
            _documentStores = documentStores;
        }


        public bool TryCreateIndex(string type, Func<JObject, IEnumerable<IIndexableField>> mapper)
        {
            lock (_indices)
            {
                return _indices.TryAdd(type, new IndexState(new RAMDirectory(), mapper));
            }
        }
        public bool IndexDocument(string type, string id, JObject document)
        {
            lock (_indices)
            {

                if (_indices.TryGetValue(type, out var index))
                {
                    index.Writer.UpdateDocument(new Term("_id", id), index.Mapper(id, document));

                    index.Writer.Commit();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool DeleteDocument(string type, string id)
        {
            lock (_indices)
            {
                if (_indices.TryGetValue(type, out var index))
                {
                    index.Writer.DeleteDocuments(new Term("_id", id));
                    index.Writer.Commit();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public SearchResult<JObject> Filter(string type, JObject query, uint size)
        {
            var result = new SearchResult<JObject>();

            if (_indices.TryGetValue(type, out var index))
            {
                using var reader = index.Writer.GetReader(true);
                var searcher = new IndexSearcher(reader);

                var filter = _filtersEngine.Parse(query);

                var docs = searcher.Search(new ConstantScoreQuery(filter.ToLuceneQuery()), (int)size);
                var ids = docs.ScoreDocs.Select(hit => searcher.Doc(hit.Doc)?.Get("_id")).NotNull().ToList();
                foreach (var store in _documentStores())
                {
                    if (store.Handles(type))
                    {
                        result.Hits = store.GetDocuments(ids);
                        result.Total = (uint)docs.TotalHits;
                        return result;
                    }
                }
            }

            result.Hits = Enumerable.Empty<Document<JObject>>();
            return result;


        }




        public bool Handles(string type)
        {
            return _indices.ContainsKey(type);
        }

        public void Dispose()
        {


            lock (_indices)
            {
                foreach (var index in _indices.Values)
                {
                    index.LuceneDirectory.Dispose();
                }
            }
        }
    }

    public interface ILuceneDocumentStore
    {
        bool Handles(string type);
        IEnumerable<Document<JObject>> GetDocuments(IEnumerable<string> ids);


    }

    public static class LuceneFilterExtensions
    {
        public static Query ToLuceneQuery(this IFilterExpression expression)
        {
            return expression switch
            {
                { Type: "match" } => ((TermFilterExpression)expression).ToLuceneQuery(),
                { Type: "bool" } => ((BoolFilterExpression)expression).ToLuceneQuery(),
                { Type: "match_all" } => ((MatchAllFilterExpression)expression).ToLuceneQuery(),
                _ => throw new NotSupportedException()
            };
        }
        internal static IEnumerable<string> NotNull(this IEnumerable<string?> source)
        {
            foreach (var item in source)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }
        public static Query ToLuceneQuery(this TermFilterExpression expression)
        {
            switch (expression.Value.Type)
            {
                case JTokenType.Integer:
                    {
                        var value = ((JValue)expression.Value).ToObject<long>();
                        return NumericRangeQuery.NewInt64Range(expression.Field, value, value, true, true);
                    }
                case JTokenType.Float:
                    {
                        var value = ((JValue)expression.Value).ToObject<double>();
                        return NumericRangeQuery.NewDoubleRange(expression.Field, value, value, true, true);
                    }

                case JTokenType.String:
                    return new TermQuery(new Term(expression.Field, expression.Value.ToObject<string>()));
                case JTokenType.Boolean:
                    {
                        var value = ((JValue)expression.Value).ToObject<bool>() ? 1 : 0;
                        return NumericRangeQuery.NewInt32Range(expression.Field, value, value, true, true);
                    }
                default:
                    throw new NotSupportedException($"'{expression.Value}' not supported as a match field value.");
            }

        }

        public static BooleanQuery ToLuceneQuery(this BoolFilterExpression expression)
        {
            var query = new BooleanQuery();

            foreach (var clause in expression.Must)
            {
                query.Add(new BooleanClause(clause.ToLuceneQuery(), Occur.MUST));
            }

            foreach (var clause in expression.MustNot)
            {
                query.Add(new BooleanClause(clause.ToLuceneQuery(), Occur.MUST_NOT));
            }
            int shouldClauses = 0;
            foreach (var clause in expression.Should)
            {
                shouldClauses++;
                query.Add(new BooleanClause(clause.ToLuceneQuery(), Occur.SHOULD));
            }

            query.MinimumNumberShouldMatch = Math.Min(expression.MinimumShouldMatch, shouldClauses);
            return query;

        }

        public static Query ToLuceneQuery(this MatchAllFilterExpression expression)
        {
            return new MatchAllDocsQuery();
        }
    }
    public interface IFilterExpressionFactory
    {
        /// <summary>
        /// Returns a boolean indicating if the factory can handle the query root element.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        bool Handles(JObject query);

        /// <summary>
        /// Returns all the expressions that can be created from the query root element.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        IEnumerable<IFilterExpression> CreateExpression(JObject query, Filters filterEngine);
    }

    public class Filters
    {
        private readonly IEnumerable<IFilterExpressionFactory> filters;

        public Filters(IEnumerable<IFilterExpressionFactory> filters)
        {
            this.filters = filters;
        }

        public IFilterExpression Parse(JObject query)
        {
            var expressions = new List<IFilterExpression>();
            foreach (var filter in filters)
            {
                if (filter.Handles(query))
                {
                    expressions.AddRange(filter.CreateExpression(query, this));
                }
            }

            if (expressions.Count == 0)
            {
                return new MatchAllFilterExpression();
            }
            else if (expressions.Count == 1)
            {
                return expressions.First();
            }
            else
            {
                return new BoolFilterExpression(expressions);
            }
        }
    }


    public interface IFilterExpression
    {
        public string Type { get; }
    }

    public class CommonFiltersExpressionFactory : IFilterExpressionFactory
    {
        private Dictionary<string, Func<JObject, Filters, IFilterExpression>> _factories = new Dictionary<string, Func<JObject, Filters, IFilterExpression>>
        {
            ["bool"] = (query, engine) => new BoolFilterExpression(query, engine),
            ["match"] = (query, _) => new TermFilterExpression(query)

        };
        public IEnumerable<IFilterExpression> CreateExpression(JObject query, Filters filtersEngine)
        {
            foreach (var factory in _factories)
            {
                if (query.ContainsKey(factory.Key))
                {
                    var args = query[factory.Key];
                    if (args == null || args.Type != JTokenType.Object)
                    {
                        throw new InvalidOperationException($"Expression config must be an object. Actual value: '{args}'");
                    }

                    yield return factory.Value((JObject)args, filtersEngine);
                }
            }
        }

        public bool Handles(JObject query)
        {
            foreach (var key in _factories.Keys)
            {
                if (query.ContainsKey(key))
                {
                    return true;
                }
            }
            return false;
        }
    }
    public class MatchAllFilterExpression : IFilterExpression
    {
        public string Type => "match_all";
    }

    public class BoolFilterExpression : IFilterExpression
    {
        public BoolFilterExpression(JObject filter, Filters engine)
        {
            var clause = filter.ToObject<BoolClause>();
            if (clause == null)
            {
                throw new InvalidOperationException("'bool' filter does not accept 'null' as arguments.");
            }
            MinimumShouldMatch = clause.minimum_should_match;

            Must = clause.must.Select(c => engine.Parse(c));
            Should = clause.should.Select(c => engine.Parse(c));
            MustNot = clause.must_not.Select(c => engine.Parse(c));
        }

        public BoolFilterExpression(IEnumerable<IFilterExpression> must)
        {
            Must = must;
            MustNot = Enumerable.Empty<IFilterExpression>();
            Should = Enumerable.Empty<IFilterExpression>();
        }


        public string Type => "bool";
        public IEnumerable<IFilterExpression> Must { get; }
        public IEnumerable<IFilterExpression> MustNot { get; }
        public IEnumerable<IFilterExpression> Should { get; }

        public int MinimumShouldMatch { get; }
    }

    public class TermFilterExpression : IFilterExpression
    {
        public string Type => "match";

        public string Field { get; }
        public JToken Value { get; }

        public TermFilterExpression(JObject filter)
        {
            var clause = filter.ToObject<MatchClause>();
            if (clause == null)
            {
                throw new InvalidOperationException("'term' filter does not accept 'null' as arguments.");
            }
            if (clause.field == null)
            {
                throw new InvalidOperationException("'term.field' cannot be null.");
            }
            if (clause.value == null)
            {
                throw new InvalidOperationException("'term.value' cannot be null.");
            }
            Field = clause.field;
            Value = clause.value;

        }

    }


}
