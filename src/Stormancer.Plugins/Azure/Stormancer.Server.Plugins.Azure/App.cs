namespace Stormancer.Server.Plugins.Azure
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EntityFrameworkCorePlugin());
        }
    }
}
