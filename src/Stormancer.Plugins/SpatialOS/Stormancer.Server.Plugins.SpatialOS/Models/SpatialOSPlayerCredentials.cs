using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.SpatialOS.Models
{
    /// <summary>
    /// Credentials used to login a player to a SpatialOS deployment
    /// </summary>
    public class SpatialOsPlayerCredentials
    {
        internal SpatialOsPlayerCredentials(string pit, string lt, DateTime expires)
        {
            PlayerIdentityToken = pit;
            LoginToken = lt;
            Expires = expires;
        }
        
        /// <summary>
        /// SpatialOS player identity token
        /// </summary>
        public string PlayerIdentityToken { get; }
        
        /// <summary>
        /// SpatialOS login token
        /// </summary>
        public string LoginToken { get; }

        /// <summary>
        /// The  login token's expiration time
        /// </summary>
        public DateTime Expires { get; }
    }
}
