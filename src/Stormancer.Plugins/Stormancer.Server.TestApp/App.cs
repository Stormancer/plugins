using System;

namespace Stormancer.Server.TestApp
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new TestPlugin());
        }
    }
}
