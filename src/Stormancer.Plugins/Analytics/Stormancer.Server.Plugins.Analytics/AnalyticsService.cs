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
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics
{
    class AnalyticsService : IAnalyticsService
    {
        private const string LOG_CATEGORY = "Analytics";
       
        private readonly ConcurrentDictionary<string, ConcurrentQueue<AnalyticsDocument>> _documents = new ConcurrentDictionary<string, ConcurrentQueue<AnalyticsDocument>>();
        private readonly IESClientFactory _cFactory;
        private readonly IEnvironment _environment;
        private readonly Task<ApplicationInfos> _applicationInfosTask;
        private readonly Task<FederationViewModel> _federation;

        
        private ILogger _logger;
        private readonly Func<IEnumerable<IAnalyticsOutput>> outputs;

        public AnalyticsService(
            IESClientFactory clientFactory,
            IEnvironment environment,
            ILogger logger,
            IConfiguration configuration,
            Func<IEnumerable<IAnalyticsOutput>> outputs)
        {
            _cFactory = clientFactory;
            _environment = environment;
            _logger = logger;
            this.outputs = outputs;
            _applicationInfosTask = _environment.GetApplicationInfos();
            _federation = _environment.GetFederation();
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

        public async Task Flush()
        {
          
            var tasks = new List<Task>();
            foreach (var kvp in _documents)
            {
                var dataType = kvp.Key;
                var queue = kvp.Value;
                List<AnalyticsDocument> documents = new List<AnalyticsDocument>();
                while (queue.TryDequeue(out var doc))
                {
                    var appInfos = await _applicationInfosTask;
                    var fed = await _federation;
                    doc.AccountId = appInfos.AccountId;
                    doc.App = appInfos.ApplicationName;
                    doc.Cluster = fed.current.id;
                    doc.DeploymentId = appInfos.DeploymentId;
                    doc.IsDeploymentActive = _environment.IsActive;
                    documents.Add(doc);
                }
                foreach(var output in outputs())
                {
                    tasks.Add(output.Flush(dataType, documents));
                }   
            }

            await Task.WhenAll(tasks);

        }
        
        /// <summary>
        /// Push data in memory
        /// </summary>        
        /// <param name="content">String to store</param>
        public void Push(AnalyticsDocument content)
        {
            var store = _documents.GetOrAdd(content.Type, t => new ConcurrentQueue<AnalyticsDocument>());
            content.CreationDate = DateTime.UtcNow;
            store.Enqueue(content);
        }

        /// <summary>
        /// Push data in memory
        /// </summary>
        /// <param name="group">Index type where the data will be store</param>
        /// <param name="category">category of analytics document, for search purpose</param>
        /// <param name="content">Json object to store</param>
        public void Push(string group,string category, JObject content)
        {
            AnalyticsDocument document = new AnalyticsDocument { Content = content, Type = group, Category = category };
            Push(document);
        }
    }
}
