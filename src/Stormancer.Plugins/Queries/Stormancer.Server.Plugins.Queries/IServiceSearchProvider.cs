using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Queries
{
    public interface IServiceSearchProvider
    {
        bool Handles(string type);
        SearchResult<JObject> Filter(string type, JObject filter, uint size);
    }
}