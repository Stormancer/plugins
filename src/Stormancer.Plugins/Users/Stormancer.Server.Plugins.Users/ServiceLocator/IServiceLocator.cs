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
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    /// <summary>
    /// Provides functions used to locate a scene running a service.
    /// </summary>
    public interface IServiceLocator
    {
        /// <summary>
        /// Creates a connection token to the scene running the specified service.
        /// </summary>
        /// <param name="serviceType"></param>
        /// <param name="serviceName"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        Task<string> GetSceneConnectionToken(string serviceType, string serviceName, Session? session);

        /// <summary>
        /// Gets the id of the scene running the specified service.
        /// </summary>
        /// <param name="serviceType"></param>
        /// <param name="serviceName"></param>
        /// <param name="session"></param>
        /// <returns></returns>
		Task<string?> GetSceneId(string serviceType, string serviceName, Session? session = null);

        /// <summary>
        /// Sends a scene to scene request to a service instance.
        /// </summary>
        /// <param name="serviceType">Type of the service.</param>
        /// <param name="serviceInstance">Id of the service instance if there are several instances of the instance type.</param>
        /// <param name="route">Route the request is sent to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task<IS2SRequest> StartS2SRequestAsync(string serviceType, string serviceInstance, string route, CancellationToken cancellationToken);


        /// <summary>
        /// Creates a scene to scene request to a service instance.
        /// </summary>
        /// <param name="serviceType">Type of the service.</param>
        /// <param name="serviceInstance">Id of the service instance if there are several instances of the instance type.</param>
        /// <param name="route">Route the request is sent to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>The request must be sent afterward.</remarks>
        /// <returns></returns>
        Task<IS2SRequest> CreateS2SRequestAsync(string serviceType, string serviceInstance, string route, CancellationToken cancellationToken);
    }
}