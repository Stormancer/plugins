using Stormancer.Server.Hosting;
using Stormancer.Server.Plugins.Tests.Sample;
public class Program
{
    public static Task Main(string[] args)
    {

        return ServerApplication.Run(builder => builder
           .Configure(args)
           .AddAllStartupActions()
        );
    }
}