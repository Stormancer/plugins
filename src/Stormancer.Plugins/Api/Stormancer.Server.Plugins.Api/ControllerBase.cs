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
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.API
{
    /// <summary>
    /// Base class for Stormancer API controllers.
    /// </summary>
    public abstract class ControllerBase
    {
        /// <summary>
        /// Gets the current Client to server request.
        /// </summary>
        public RequestContext<IScenePeerClient>? Request { get; internal set; }

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> of the current request.
        /// </summary>
        [Obsolete("Use the cancellation token in the request object.")]
        public CancellationToken? CancellationToken { get; internal set; }

        /// <summary>
        /// Gets the peer for a S2S request.
        /// </summary>
        [Obsolete]
        public IScenePeer? Peer { get; internal set; }

        /// <summary>
        /// Gets a value indicating if the current request is an S2S request.
        /// </summary>
        [Obsolete]
        public bool IsS2S
        {
            get
            {
                return !(Peer is IScenePeerClient);
            }
        }
        /// <summary>
        /// Handles exceptions uncatched by the route itself.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns>A task that returns true if the exception was handled, false otherwise.</returns>
        protected internal virtual Task<bool> HandleException(ApiExceptionContext ctx)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// An user started a connection attempt.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        protected internal virtual Task OnConnecting(IScenePeerClient client)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// An user's connection was rejected.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        protected internal virtual Task OnConnectionRejected(IScenePeerClient client)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// An user successfully connected to the scene.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected internal virtual Task OnConnected(IScenePeerClient peer)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// An user disconnected from the scene.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected internal virtual Task OnDisconnected(DisconnectedArgs args)
        {
            return Task.FromResult(true);
        } 
    }
}
