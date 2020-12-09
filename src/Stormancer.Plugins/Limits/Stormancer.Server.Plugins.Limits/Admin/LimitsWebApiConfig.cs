using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Stormancer.Server.Plugins.AdminApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    class LimitsAdminWebApiConfig : IAdminWebApiConfig
    {
        void IAdminWebApiConfig.ConfigureApplicationParts(ApplicationPartManager apm)
        {
            apm.ApplicationParts.Add(new AssemblyPart(this.GetType().Assembly));
        }
    }
}
