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

using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    /// <summary>
    /// Context passed to a service location request.
    /// </summary>
    public class ServiceLocationCtx
    {
        /// <summary>
        /// Type of the service
        /// </summary>
        public string ServiceType { get; set; } = default!;

        /// <summary>
        /// Name of the specific instance of the service we are trying to locate
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// The id of the scene the service is located.
        /// </summary>
        public string? SceneId { get; set; }

        /// <summary>
        /// Additional context for the request.
        /// </summary>
        public Dictionary<string, string> Context { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Session of the user doing the request.
        /// </summary>
        public Session? Session { get;  set; }
    }

    /// <summary>
    /// Classes implementing this interface can participate in the service location process.
    /// </summary>
    public interface IServiceLocatorProvider
    {
        /// <summary>
        /// Tries to locate a service.
        /// </summary>
        /// <param name="ctx">Service location context.</param>
        /// <returns>A task that completes when the method completes.</returns>
        Task LocateService(ServiceLocationCtx ctx);
    }
}
