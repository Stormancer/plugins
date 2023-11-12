using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
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
            endpoints.MapPost("/clients/connect", async (ApplicationConfigurationOptions parameters, [FromServices] ClientsManager clientsManager, CancellationToken cancellation) =>
            {
                await clientsManager.ConnectAsync(parameters, false,cancellation);
            }).RequireAuthorization();

            endpoints.MapPost("/clients/stop", async (ApplicationConfigurationOptions parameters, [FromServices] ClientsManager clientsManager) =>
            {
                await clientsManager.StopAsync(parameters);
            }).RequireAuthorization();


            endpoints.MapGet("/clients", async ([FromServices] ClientsManager clientsManager) =>
            {
                return clientsManager.GetClients();
            }).RequireAuthorization();
        }
    }


}
