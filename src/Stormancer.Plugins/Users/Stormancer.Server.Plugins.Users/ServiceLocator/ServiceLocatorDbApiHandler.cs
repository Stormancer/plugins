using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    class ServiceLocatorDbApiHandler : IApiHandler
    {
        private readonly ServiceLocatorHostDatabase db;

        public ServiceLocatorDbApiHandler(ServiceLocatorHostDatabase db)
        {
            this.db = db;
        }

        /// <summary>
        /// Called for each controller on a scene when it is created.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public Task SceneStarting(ISceneHost scene, ControllerBase controller)
        {
            var serviceAttribute = controller.GetType().GetCustomAttribute<ServiceAttribute>();
            if (serviceAttribute != null)
            {
                if (controller is IServiceMetadataProvider metadataProvider)
                {
                    db.AddScene(serviceAttribute.ServiceType ?? GetDefaultServiceType(controller.GetType()), metadataProvider.GetServiceInstanceId(scene), scene);
                }
                else
                {
                    db.AddScene(serviceAttribute.ServiceType ?? GetDefaultServiceType(controller.GetType()), string.Empty, scene);
                }
            }
            
            return Task.CompletedTask;
        }
        private string GetDefaultServiceType(Type controllerType)
        {
            var name = controllerType.FullName;
            Debug.Assert(name != null);

            if(name.EndsWith("Controller"))
            {
                name = name.Substring(0, name.Length - "Controller".Length);
            }
            return name;
        }

        
   
    }
}
