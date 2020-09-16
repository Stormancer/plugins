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
using Stormancer.Diagnostics;
using Stormancer.Server;
using Stormancer.Server.Plugins.Management;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extensions to the <see cref="IHost"/> class.
    /// </summary>
    public static class HostExtensions
    {

        /// <summary>
        /// Ensures a scene is created on the server.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="sceneUri"></param>
        /// <param name="templateId"></param>
        /// <param name="isPublic"></param>
        /// <param name="isPersistent"></param>
        /// <param name="metadata"></param>
        public static void EnsureSceneExists(this IHost host, string sceneUri, string templateId, bool isPublic , bool isPersistent, JObject? metadata = null )
        {
            async Task createScene()
            {
                try
                {
                    await host.DependencyResolver.Resolve<ManagementClientProvider>().CreateScene(sceneUri, templateId, isPublic, isPersistent, metadata);
                }
                catch(Exception ex)
                {
                    host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, "management", $"Failed to create scene {sceneUri} of template {templateId}.", ex);
                }
            }

            _ = createScene();
        }
    }
}
