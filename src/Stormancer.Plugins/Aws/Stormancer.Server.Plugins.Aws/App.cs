namespace Stormancer.Server.Plugins.Aws
{
    public class App
    {
        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new AwsPlugin());
        }
    }
}
