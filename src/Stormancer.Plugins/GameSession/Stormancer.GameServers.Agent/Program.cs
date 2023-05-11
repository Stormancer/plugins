using Docker.DotNet.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Stormancer.GameServers.Agent;

DockerAgentConfigurationOptions? options = null;

IHost host = Host.CreateDefaultBuilder()
    .ConfigureHostConfiguration((configBuilder)=>
    {
        configBuilder.Sources.Clear();
        configBuilder.AddJsonFile(Path.Combine(Environment.CurrentDirectory,"appsettings.json"), optional: false, reloadOnChange: true)
        .AddEnvironmentVariables(prefix:"STRM_GS_AGENT_")
        .AddCommandLine(args);
        
    })
    .ConfigureAppConfiguration((hostingContext,configBuilder)=>
    {
        var env = hostingContext.HostingEnvironment;
        configBuilder
              .AddJsonFile(Path.Combine(Environment.CurrentDirectory, $"appsettings.{env.EnvironmentName}.json"), optional: true, reloadOnChange: true);

        options = configBuilder.Build().GetSection(DockerAgentConfigurationOptions.Section).Get<DockerAgentConfigurationOptions>();
    })
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

            
            kestrel.ListenAnyIP(options?.HttpPort??30001);
            });
    })
    
    .Build();

await host.RunAsync();
