namespace Stormancer.Server.Plugins.BlobStorage
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EntityFrameworkCorePlugin());
        }
    }
}
