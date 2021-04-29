using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server
{
    /// <summary>
    /// Annotate an S2S request context argument in a S2S action to provide
    /// </summary>
    public class S2SContextUsageAttribute : Attribute
    {
        /// <summary>
        /// Create an instance of <see cref="S2SContextUsageAttribute"/>.
        /// </summary>
        /// <param name="usage"></param>
        public S2SContextUsageAttribute(S2SRequestContextUsage usage)
        {
            Usage = usage;
        }

        /// <summary>
        /// Gets the expected usage of the S2SRequestContext.
        /// </summary>
        public S2SRequestContextUsage Usage { get; }
    }

    /// <summary>
    /// Usage of the request context.
    /// </summary>
    [Flags]
    public enum S2SRequestContextUsage
    {
        /// <summary>
        /// Signals that the action does not read nor write data manually to the request context.
        /// </summary>
        None = 0,
        /// <summary>
        /// Signals that the action reads from the request context input pipe.
        /// </summary>
        /// <remarks>
        /// The API framework reads the actions arguments then pass the request context to the action for further reading.
        /// So if the action has other arguments than the context, they will be read before the context is made available to the action.
        /// </remarks>
        Read = 1,

        /// <summary>
        /// Signals that the action writes manually to the request context output pipe.
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        Write = 2
    }
}
