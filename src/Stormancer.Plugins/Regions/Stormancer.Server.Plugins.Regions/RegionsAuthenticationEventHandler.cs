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
using Stormancer.Server.Plugins.Users;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Regions
{

    internal class RegionsAuthenticationEventHandler : IAuthenticationEventHandler
    {
        private readonly RegionsTestingService _regionTestingService;
        private readonly IUserSessions _sessions;
        private readonly IConfiguration _configuration;

        public RegionsAuthenticationEventHandler(RegionsTestingService storage, IUserSessions sessions, IConfiguration configuration)
        {
            _regionTestingService = storage;
            _sessions = sessions;
            _configuration = configuration;
        }
        public async Task OnLoggedIn(Stormancer.Server.Plugins.Users.LoggedInCtx ctx)
        {
            var config = _configuration.GetValue<RegionsConfigurationSection>("regions");
            if (config.Enabled && ctx.Peer.Routes.Any(r=>r.Name == "regions.testIps"))
            {
                var testResults = await ctx.Peer.RpcTask<LatencyTestRequest, GetLatencyTestsResponse>("regions.testIps", new LatencyTestRequest { TestIps = await _regionTestingService.GetTestIps() }, ctx.CancellationToken);

                var regionPreferences = testResults.Results.OrderBy(kvp => kvp.Latency).Select(kvp => kvp.Region);

                await _regionTestingService.UpdateRegionAsync(ctx.Session.SessionId, regionPreferences, ctx.CancellationToken);
            }
        }
    }
}