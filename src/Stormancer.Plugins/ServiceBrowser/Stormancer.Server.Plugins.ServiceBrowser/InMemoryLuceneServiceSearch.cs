using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.ServiceBrowser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.ServiceQueries
{
    internal class Index : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

      
    }

    class InMemoryLuceneServiceSearch : IServiceSearchProvider
    {
        public InMemoryLuceneServiceSearch()
        {

        }

        public void CreateIndex(string index)
        {
            // Ensures index backward compatibility
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

            // Construct a machine-independent path for the index
            var basePath = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
          

            using var dir = new RAMDirectory();

            // Create an analyzer to process the text
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
            using var writer = new IndexWriter(dir, indexConfig);

        }


        public IEnumerable<Document<JObject>> Filter(JObject filter, uint size)
        {
            throw new NotImplementedException();
        }

        public bool Handles(string type)
        {
            throw new NotImplementedException();
        }
    }
}
