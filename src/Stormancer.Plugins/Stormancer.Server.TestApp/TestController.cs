using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    [Service(ServiceType = "tests.entry")]
    class TestController : ControllerBase
    {
        private readonly S2SProxy proxy;

        public TestController(S2SProxy proxy)
        {
            this.proxy = proxy;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task TestS2SAsyncEnumerable()
        {
            await foreach (var _ in proxy.AsyncEnumerable(CancellationToken.None))
            {

            }
        }
    }
}
