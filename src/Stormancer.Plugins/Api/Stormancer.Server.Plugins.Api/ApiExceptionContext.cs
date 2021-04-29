// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using Stormancer;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.API
{
    /// <summary>
    /// Context for exception handling in controllers.
    /// </summary>
    public class ApiExceptionContext
    {
        internal ApiExceptionContext(string route, Exception exception, RequestContext<IScenePeerClient> request) : this(route, exception)
        {
            Request = request;
        }

        internal ApiExceptionContext(string route, Exception exception, Packet<IScenePeerClient> packet) : this(route, exception)
        {
            Packet = packet;
        }

        internal ApiExceptionContext(string route, Exception exception, IS2SRequestContext ctx) : this(route, exception)
        {

        }
        private ApiExceptionContext(string route, Exception exception)
        {
            this.Route = route;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets the thrown <see cref="Exception"/>.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the route the exception was thrown on.
        /// </summary>
        public string Route { get; }

        /// <summary>
        /// Gets the request that threw the exception (for client to server requests).
        /// </summary>
        public RequestContext<IScenePeerClient>? Request { get; }

        /// <summary>
        /// Gets the packet that threw th exception (for client to server messages).
        /// </summary>
        public Packet<IScenePeerClient>? Packet { get; }

        /// <summary>
        /// Gets the S2S request context associated with the call if it's an S2S call.
        /// </summary>
        public IS2SRequestContext? S2SRequest { get; }

        /// <summary>
        /// Is the call an RPC.
        /// </summary>
        public bool IsRpc => Request != null;

        /// <summary>
        /// Is the call a fire &amp; forget packet
        /// </summary>
        public bool IsRoute => Packet != null;

        /// <summary>
        /// Is the Call an S2S request.
        /// </summary>
        public bool IsS2SRequest => Packet != null;
    }

}
