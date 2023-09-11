using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    internal class CollectionProfilePartBuilder : IProfilePartBuilder
    {
        private readonly ICollectionService _service;

        public CollectionProfilePartBuilder(ICollectionService service)
        {
            _service = service;
        }

        public async Task GetProfiles(ProfileCtx ctx, CancellationToken cancellationToken)
        {
            if(ctx.DisplayOptions.ContainsKey("collection"))
            {
                var results = await _service.GetCollectionAsync(ctx.Users, cancellationToken);

                foreach(var (userId,collection) in results)
                {
                    ctx.UpdateProfileData(userId, "collection", obj => {
                        obj.Add("items", new JArray(collection));
                        return obj;
                     });
                }
            }
        }
    }
}
