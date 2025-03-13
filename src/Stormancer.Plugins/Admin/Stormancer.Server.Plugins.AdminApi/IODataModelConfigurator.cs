using Microsoft.OData.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.WebApi
{
    /// <summary>
    /// Extending the API enables building the web API OData model.
    /// </summary>
    public interface IODataModelConfigurator
    {
        /// <summary>
        /// Configures the OData Model for the admin Web API.
        /// </summary>
        /// <param name="builder"></param>
        void ConfigureAdminODataModel(ODataConventionModelBuilder builder);
       
    }
}
