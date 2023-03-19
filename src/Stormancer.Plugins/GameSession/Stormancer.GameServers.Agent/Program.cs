using Stormancer.GameServers.Agent;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<Messager>();
        services.AddSingleton<DockerService>();
        services.AddSingleton<PortsManager>();
        services.AddSingleton<AgentController>();
        
    })
    .Build();

await host.RunAsync();
