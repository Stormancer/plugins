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

using Stormancer.Server.Plugins.Configuration;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics
{
    class ElasticsearchOutput : IAnalyticsOutput
    {
        private const string TABLE_NAME = "analytics";

        private readonly IESClientFactory clientFactory;
        private readonly IConfiguration configuration;
        private readonly ILogger logger;

        public ElasticsearchOutput(IESClientFactory clientFactory, IConfiguration configuration, ILogger logger)
        {
            this.clientFactory = clientFactory;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task Flush(string store, IEnumerable<AnalyticsDocument> docs)
        {
            var client = await CreateESClient(store);

            if (docs.Count() > 0)
            {
               
                var r = await client.BulkAsync(bd => bd.IndexMany<AnalyticsDocument>(docs));
                //logger.Log(LogLevel.Info, "analytics", "saved analytics", new { debug = r.DebugInformation });
              
                if (r.Errors)
                {
                    
                    logger.Log(LogLevel.Error, "analytics", "Failed to index analytics", new { errors = r.ItemsWithErrors.Select(i => i.Error.ToString()) , r.ServerError });
                }
            }
        }

        private async Task<Nest.IElasticClient> CreateESClient(string type, string param = "")
        {
            var result = await clientFactory.CreateClient(type, TABLE_NAME, param);
            return result;
        }


        /// <summary>
        /// Get week number.
        /// </summary>
        /// <param name="date"></param>
        /// <returns>return weak number</returns>
        private long GetWeek(DateTime date)
        {
            return date.Ticks / (TimeSpan.TicksPerDay * 7);
        }

    }
}
