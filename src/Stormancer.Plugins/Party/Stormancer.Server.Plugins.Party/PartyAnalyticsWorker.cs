using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        internal async Task Run()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var count = 0;
                var players = 0;
                lock(_syncRoot)
                {
                    count = _parties.Count;
                    players = _parties.Sum(p=>p.PartyMembers.Count);
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

            var duration = DateTime.UtcNow - startTime;

            if (TimeSpan.FromSeconds(1) - duration > TimeSpan.FromMilliseconds(20))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - duration);
            }
        }
    }
}
