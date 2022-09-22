using Stormancer.Server.Hosting;
using System.Threading.Tasks;
using Stormancer.Plugins.Tests.ServerApp;
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

