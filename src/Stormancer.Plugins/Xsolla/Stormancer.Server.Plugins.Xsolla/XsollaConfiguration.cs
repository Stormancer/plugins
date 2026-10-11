using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Xsolla
{
    /// <summary>
    /// Configuration of the Xsolla plugin.
    /// </summary>
    public class XsollaConfiguration : IConfigurationSection<XsollaConfiguration>
    {
        public static string SectionPath => "xsolla";

        public static XsollaConfiguration Default => new XsollaConfiguration();

        public string ProjectId { get; set; }
    }
}
