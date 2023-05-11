using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Stormancer.GameServers.Agent;

var config = new ConfigurationBuilder()
 .AddJsonFile("appsettings.json")
.AddEnvironmentVariables()
.AddCommandLine(args)
.Build();

IHost host = Host.CreateDefaultBuilder()
    .ConfigureHostConfiguration(configBuilder=> configBuilder.AddConfiguration(config))
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<Messager>();
        services.AddSingleton<DockerService>();
        services.AddSingleton<PortsManager>();
        services.AddSingleton<AgentController>();
        
    })
    .ConfigureWebHostDefaults(webBuilder=>
    {
        webBuilder.UseConfiguration(config);
        webBuilder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => {


                endpoints.MapGet("/", () =>
                {
                    return Results.Ok();
                });
            });
        });
        webBuilder.UseKestrel(kestrel => {

            var section = config.GetSection(DockerAgentConfigurationOptions.Section).Get<DockerAgentConfigurationOptions>();
            kestrel.ListenAnyIP(section?.HttpPort??30001);
            });
    })
    .Build();

await host.RunAsync();
