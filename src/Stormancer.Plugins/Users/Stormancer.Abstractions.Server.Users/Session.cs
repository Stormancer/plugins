using MessagePack;
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
    [MessagePackObject]
    public class Session
    {
        /// <summary>
        /// Creates a new <see cref="Session"/>.
        /// </summary>
        public Session() 
        { 
        }
        /// <summary>
        /// Gets the main platform id associated with the session.
        /// </summary>
        [Key(0)]
        public PlatformId platformId { get; set; }

        /// <summary>
        /// Gets the user associated with the session if it exists.
        /// </summary>
        [Key(1)]
        public User? User { get; set; }

        /// <summary>
        /// Gets the session id.
        /// </summary>
        [Key(2)]
        public SessionId SessionId { get; set; }

        /// <summary>
        /// List of identities of the session.
        /// </summary>
        [Key(3)]
        public IReadOnlyDictionary<string, string> Identities { get; set; } = default!;

        /// <summary>
        /// Gets session data associated with the session.
        /// </summary>
        [Key(4)]
        public IReadOnlyDictionary<string, byte[]> SessionData { get;  set; } = default!;

        /// <summary>
        /// Gets the <see cref="DateTime"/> object representing when the session was created.
        /// </summary>
        [Key(5)]
        public DateTime ConnectedOn { get;  set; }

        /// <summary>
        /// absolute scene Url to the authenticator scene
        /// </summary>
        [Key(6)]
        public string AuthenticatorUrl { get; set; } = default!;

        /// <summary>
        /// If the session is cached, the date at which it should expire
        /// </summary>
        [Key(7)]
        public DateTimeOffset? MaxAge { get;  set; }

        /// <summary>
        /// Version of the document.
        /// </summary>
        [Key(8)]
        public uint Version { get;  set; }
    }
}
