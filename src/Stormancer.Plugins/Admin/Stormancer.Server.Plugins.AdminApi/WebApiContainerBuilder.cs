using Stormancer.Core;
using Stormancer.Server.Admin;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.WebApi
{
    internal class WebApiContainerBuilder : IWebApiContainerBuilder
    {
        private List<Action<IDependencyBuilder>> _actions = new List<Action<IDependencyBuilder>>();
        public void AddBuildAction(Action<IDependencyBuilder> buildAction)
        {
            _actions.Add(buildAction);
        }

        public IDependencyResolver CreateResolver(IDependencyResolver rootResolver)
        {
            return rootResolver.CreateChild(Constants.ApiRequestTag, builder =>
            {
                foreach(var action in _actions) {
                    action(builder);
                }
            });
        }
    }
}
