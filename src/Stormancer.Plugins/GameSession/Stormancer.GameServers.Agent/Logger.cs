using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class Logger : Stormancer.Diagnostics.ILogger
    {
        private readonly ILogger logger;
        internal static EventId Stormancer = new(1000, "Stormancer");
        public Logger(ILogger logger)
        {
            this.logger = logger;
        }
        public void Log(Diagnostics.LogLevel level, string category, string message, object data = null)
        {
            logger.Log(GetLogLevel(level), Stormancer, (category, message), null, (tuple, ex) =>
            {
                var (cat, msg) = tuple;
                return $"{cat}:{msg}";
            });
        }

        public LogLevel GetLogLevel(Diagnostics.LogLevel level)
        {
            var v = (int)(level);
            return (LogLevel)(5 - v);
        }
    }
}