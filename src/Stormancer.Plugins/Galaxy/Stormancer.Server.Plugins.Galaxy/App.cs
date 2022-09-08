namespace Stormancer.Server.Plugins.Galaxy
{
    /// <summary>
    /// Plugin initialization class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Initialization method.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new GalaxyPlugin());
        }
    }
}
