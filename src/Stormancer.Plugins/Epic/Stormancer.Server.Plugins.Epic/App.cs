namespace Stormancer.Server.Plugins.Eos
{
    /// <summary>
    /// App
    /// </summary>
    public class App
    {
        /// <summary>
        /// Run
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EosPlugin());
        }
    }
}
