using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    internal class PartyAnalyticsWorker
    {
        private readonly IAnalyticsService _analytics;
        private List<PartyService> _parties = new List<PartyService>();
        private object _syncRoot = new object();

        public PartyAnalyticsWorker(IAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        public void AddParty(PartyService party)
        {
            lock(_syncRoot)
            {
                _parties.Add(party);
            }
        }

        public void RemoveParty(PartyService party)
        {
            lock(_syncRoot)
            {
                _parties.Remove(party);
            }
        }
        internal async Task Run(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!cancellationToken.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(cancellationToken);
                try
                {
                    var count = 0;
                    var players = 0;
                    lock (_syncRoot)
                    {
                        count = _parties.Count;
                        players = _parties.Sum(p => p.PartyMembers.Count);
                    }
                    if (count > 0)
                    {
                        _analytics.Push("party", "party-stats", JObject.FromObject(new
                        {
                            parties = count,
                            playersInParties = players
                        }));
                    }
                }
                catch { }

               
            }
        }
    }
}
