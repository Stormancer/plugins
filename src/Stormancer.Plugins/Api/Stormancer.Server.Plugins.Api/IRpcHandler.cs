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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.API
{
    /// <summary>
    /// An api call context.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ApiCallContext<T>
    {
        internal ApiCallContext(T ctx, MethodInfo method, string route)
        {
            Context = ctx;
            Method = method;
            Route = route;
            Builder = builder => { };

        }
        /// <summary>
        /// Gets the context of the API call.
        /// </summary>
        public T Context { get; }

        /// <summary>
        /// Gets an <see cref="MethodInfo"/> object describing the current operation.
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// Gets the route hit by the API call.
        /// </summary>
        public string Route { get; }

        /// <summary>
        /// Gets or sets the dependency builder function that will be passed to <see cref="IDependencyResolver.CreateChild(Action{IDependencyBuilder})"/> when creating the Request scope.
        /// </summary>
        public Action<IDependencyBuilder> Builder { get; set; }


    }

    /// <summary>
    /// Provides extensibility points in the Api pipeline.
    /// </summary>
    public interface IApiHandler
    {
        /// <summary>
        /// Called when a player connects to a scene.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        Task OnConnected(IScenePeerClient client) => Task.CompletedTask;

        /// <summary>
        /// Called when a player disconnects from a scene.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        Task OnDisconnected(IScenePeerClient client) => Task.CompletedTask;

        /// <summary>
        /// Called when a client RPC is executed on a scene.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        Task RunRpc(ApiCallContext<RequestContext<IScenePeerClient>> ctx, Func<ApiCallContext<RequestContext<IScenePeerClient>>, Task> next) => next(ctx);
        
        /// <summary>
        /// Runs when an S2S Request using the obsolete S2S API pattern is executed.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        Task RunRpc(ApiCallContext<RequestContext<IScenePeer>> ctx, Func<ApiCallContext<RequestContext<IScenePeer>>, Task> next) => next(ctx);

        /// <summary>
        /// Called when a S2S fire and forget message is handled.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        Task RunFF(ApiCallContext<Packet<IScenePeer>> ctx, Func<ApiCallContext<Packet<IScenePeer>>, Task> next) => next(ctx);

        /// <summary>
        /// Called when a client fire and forget message is handled.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        Task RunFF(ApiCallContext<Packet<IScenePeerClient>> ctx, Func<ApiCallContext<Packet<IScenePeerClient>>, Task> next) => next(ctx);

        /// <summary>
        /// Called when a S2S request is executed.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        Task RunS2S(ApiCallContext<IS2SRequestContext> ctx, Func<ApiCallContext<IS2SRequestContext>, Task> next) => next(ctx);

        /// <summary>
        /// Called for each controller on a scene when it is created.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        Task SceneStarting(ISceneHost scene, ControllerBase controller) => Task.CompletedTask;

        /// <summary>
        /// Called for each controller on a scene when it is shutting down.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        Task SceneShuttingDown(ISceneHost scene, ControllerBase controller) => Task.CompletedTask;
    }
}
