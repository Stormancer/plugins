using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Contains party settings
    /// </summary>
    public class ServerPartySettings : Dictionary<string, string>
    {
        /// <summary>
        /// Creates a new <see cref="ServerPartySettings"/>.
        /// </summary>
        public ServerPartySettings() { }

        /// <summary>
        /// Creates a new <see cref="ServerPartySettings"/>.
        /// </summary>
        public ServerPartySettings(Dictionary<string, string> settings) : base(settings) { }
    }
}
