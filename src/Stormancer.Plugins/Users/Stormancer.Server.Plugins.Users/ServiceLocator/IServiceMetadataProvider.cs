using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.API
{

    /// <summary>
    /// Provides a way for controller instances to provide service names.
    /// </summary>
    public interface IServiceMetadataProvider
    {
        /// <summary>
        /// Gets the name of the service. Defaults to <see cref="string.Empty"/>.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        string GetServiceInstanceId(ISceneHost scene);
    }
}
