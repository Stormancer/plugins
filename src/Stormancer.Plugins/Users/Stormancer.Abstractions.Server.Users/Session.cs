using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Represents a session.
    /// </summary>
    public class Session
    {
        public Session() 
        { 
        }
        /// <summary>
        /// Gets the main platform id associated with the session.
        /// </summary>
        public PlatformId platformId { get; set; }

        /// <summary>
        /// Gets the user associated with the session if it exists.
        /// </summary>
        public User? User { get; set; }

        /// <summary>
        /// Gets the session id.
        /// </summary>
        public SessionId SessionId { get; set; }

        /// <summary>
        /// List of identities of the session.
        /// </summary>
        public IReadOnlyDictionary<string, string> Identities { get; set; } = default!;

        /// <summary>
        /// Gets session data associated with the session.
        /// </summary>
        public IReadOnlyDictionary<string, byte[]> SessionData { get;  set; } = default!;

        /// <summary>
        /// Gets the <see cref="DateTime"/> object representing when the session was created.
        /// </summary>
        public DateTime ConnectedOn { get;  set; }

        /// <summary>
        /// absolute scene Url to the authenticator scene
        /// </summary>
        public string AuthenticatorUrl { get; set; } = default!;

        /// <summary>
        /// If the session is cached, the date at which it should expire
        /// </summary>
        public DateTimeOffset? MaxAge { get;  set; }

        /// <summary>
        /// Version of the document.
        /// </summary>
        public uint Version { get;  set; }
    }
}
