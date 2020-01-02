using Stormancer;

namespace Stormancer.Server.Plugins.Analytics
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new AnalyticsPlugin());
        }
    }
}
