using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class AdminApi
    {
        public static void Map(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/applications/connect", async (ApplicationConfigurationOptions parameters, [FromServices] ClientsManager clientsManager) =>
            {
                await clientsManager.ConnectAsync(parameters, false);
            }).RequireAuthorization();
        }
    }


}
