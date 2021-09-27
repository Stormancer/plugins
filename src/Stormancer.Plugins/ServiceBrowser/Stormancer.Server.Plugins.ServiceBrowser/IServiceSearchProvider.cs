using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.ServiceBrowser
{
    public interface IServiceSearchProvider
    {
        bool Handles(string type);
        IEnumerable<Document<JObject>> Filter(JObject filter);
    }
}