﻿using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GeoIp.Maxmind
{
    /// <summary>
    /// Adds country &amp; continent codes to the session.
    /// </summary>
    /// <remarks>
    /// Priority 1 to happen before analytics gathering.
    /// </remarks>
    [Priority(1)]
    internal class UserSessionEventHandler : IUserSessionEventHandler
    {
        private readonly IPeerInfosService _peerInfosService;
        private readonly IGeoIpService _geoIpService;

        public UserSessionEventHandler(IPeerInfosService peerInfosService, IGeoIpService geoIpService)
        {
            _peerInfosService = peerInfosService;
            _geoIpService = geoIpService;
        }

        public async Task OnLoggedIn(LoginContext ctx)
        {
            var details = await _peerInfosService.GetPeerDetails(ctx.Client);
            var countryResult = await _geoIpService.GetCountryAsync(details.IPAddress);
            var countryCode = countryResult?.Country ?? string.Empty;
            var continentCode = countryResult?.Continent ?? string.Empty;

            ctx.Session.SessionData["geoIp.countryCode"] = Encoding.ASCII.GetBytes(countryCode);
            ctx.Session.SessionData["geoIp.continent"] = Encoding.ASCII.GetBytes(continentCode);

            ctx.Dimensions["countryCode"] = countryCode;
            ctx.Dimensions["continentCode"] = continentCode;
        }
    }
}
