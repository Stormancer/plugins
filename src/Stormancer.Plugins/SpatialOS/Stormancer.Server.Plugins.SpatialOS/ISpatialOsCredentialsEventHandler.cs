using Stormancer.Server.Plugins.SpatialOS.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.SpatialOS
{
    /// <summary>
    /// Event handler called before creating SpatialOS credentials
    /// </summary>
    public interface ISpatialOsCredentialsEventHandler
    {
        /// <summary>
        /// Called before creating SpatialOS credentials
        /// </summary>
        /// <param name="context">the context of the credentials creation</param>
        /// <returns></returns>
        Task OnCreatingSpatialOsCredentials(SpatialOsPlayerCredentialsCtx context);
    }
}
