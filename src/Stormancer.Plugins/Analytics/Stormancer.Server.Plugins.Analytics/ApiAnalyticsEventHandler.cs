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
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nest;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// Configuration of the API analytics system.
    /// </summary>
    /// <remarks>
    /// The API analytics system stores event related to all network API calls, C2S &amp; S2S.
    /// Config Path: "instrumentation": { "EnableApiInstrumentation":true|false}
    /// </remarks>
    public class InstrumentationConfig : IConfigurationSection<InstrumentationConfig>
    {
        ///<inheritdoc/>
        public static string SectionPath => "instrumentation";

        ///<inheritdoc/>
        public static InstrumentationConfig Default { get; } = new InstrumentationConfig();

        /// <summary>
        /// Enables API instrumentation.
        /// </summary>
        /// <remarks>
        /// Disabled by default.
        /// </remarks>
        public bool EnableApiInstrumentation { get; set; } = false;

        /// <summary>
        /// Enables S2S instrumentation.
        /// </summary>
        /// <remarks>
        /// Disabled by default.
        /// </remarks>
        public bool EnableS2SInstrumentation { get; set; } = false;

        /// <summary>
        /// Enables tracking of scene connection/disconnection events.
        /// </summary>
        public bool EnableSceneEventsInstrumentation { get; set; } = false;
    }

   
    internal class ApiAnalyticsEventHandler : IApiHandler
    {
        private readonly IAnalyticsService _analytics;
        private readonly ConfigurationMonitor<InstrumentationConfig> _config;
        private readonly Stopwatch _watch = new Stopwatch();

       
     

        public ApiAnalyticsEventHandler(IAnalyticsService analytics,ConfigurationMonitor<InstrumentationConfig> configuration)
        {
            _analytics = analytics;
            _watch.Start();

            this._config = configuration;
            
        }



        //public Task OnStart(SearchStartContext searchStartCtx)
        //{
        //    foreach (var user in searchStartCtx.Groups.SelectMany(g => g.Players.Select(player => new { player, group = g })))
        //    {
        //        _analytics.Push("gameFinder", "start", JObject.FromObject(new { searchStartCtx.GameFinderId, user.player.UserId, groupSize = user.group.Players.Count() }));
        //    }
        //    return Task.CompletedTask;
        //}

        //public Task OnEnd(SearchEndContext searchEndCtx)
        //{
        //    foreach (var user in searchEndCtx.Group.Players)
        //    {
        //        _analytics.Push("gameFinder", "end", JObject.FromObject(new { searchEndCtx.GameFinderId, searchEndCtx.Reason, searchEndCtx.PassesCount, user.UserId }));
        //    }
        //    return Task.CompletedTask;
        //}

        //public Task OnGameStarted(GameStartedContext searchGameStartedCtx)
        //{
        //    _analytics.Push("gameFinder", "gameFound", JObject.FromObject(new { searchGameStartedCtx.GameFinderId, playerCount = searchGameStartedCtx.Game.AllPlayers.Count(), parameters = searchGameStartedCtx.Game.CustomData, gameId = searchGameStartedCtx.Game.Id }));
        //    return Task.CompletedTask;
        //}

        public Task RunRpc(ApiCallContext<RequestContext<IScenePeerClient>> ctx, Func<ApiCallContext<RequestContext<IScenePeerClient>>, Task> next)
        {
            if (_config.Value.EnableApiInstrumentation)
            {
                static async Task RunThenNext(Stopwatch watch, IAnalyticsService analytics, Func<ApiCallContext<RequestContext<IScenePeerClient>>, Task> next, ApiCallContext<RequestContext<IScenePeerClient>> ctx)
                {
                    var start = watch.ElapsedMilliseconds;
                    await next(ctx);
                    analytics.Push("api", "rpc.cs", JObject.FromObject(new
                    {
                        type = "RPC",
                        scope = "ClientServer",
                        inputSize = ctx.Context.InputStream.Length,
                        route = ctx.Route,
                        duration = watch.ElapsedMilliseconds - start,
                        SessionId = ctx.Context.RemotePeer.SessionId.ToString()
                    }));
                }
                return RunThenNext(_watch, _analytics, next, ctx);
            }
            else
            {
                return next(ctx);
            }


        }

        public Task RunRpc(ApiCallContext<RequestContext<IScenePeer>> ctx, Func<ApiCallContext<RequestContext<IScenePeer>>, Task> next)
        {
            if (_config.Value.EnableS2SInstrumentation)
            {
                static async Task RunThenNext(Stopwatch watch, IAnalyticsService analytics, Func<ApiCallContext<RequestContext<IScenePeer>>, Task> next, ApiCallContext<RequestContext<IScenePeer>> ctx)
                {
                    var start = watch.ElapsedMilliseconds;
                    await next(ctx);
                    analytics.Push("api", "rpc.s2s", JObject.FromObject(new
                    {
                        type = "RPC",
                        scope = "S2S",
                        inputSize = ctx.Context.InputStream.Length,
                        route = ctx.Route,
                        duration = watch.ElapsedMilliseconds - start,
                        SessionId = ctx.Context.RemotePeer.SceneId
                    }));
                }
                return RunThenNext(_watch, _analytics, next, ctx);
            }
            else
            {
                return next(ctx);
            }
        }

        public Task RunFF(ApiCallContext<Packet<IScenePeer>> ctx, Func<ApiCallContext<Packet<IScenePeer>>, Task> next)
        {
            if (_config.Value.EnableS2SInstrumentation)
            {
                static async Task RunThenNext(Stopwatch watch, IAnalyticsService analytics, Func<ApiCallContext<Packet<IScenePeer>>, Task> next, ApiCallContext<Packet<IScenePeer>> ctx)
                {
                    var start = watch.ElapsedMilliseconds;
                    await next(ctx);
                    analytics.Push("api", "ff.s2s", JObject.FromObject(new
                    {
                        type = "FireForget",
                        scope = "S2S",
                        inputSize = ctx.Context.Stream.Length,
                        route = ctx.Route,
                        duration = watch.ElapsedMilliseconds - start,
                        SessionId = ctx.Context.Connection.SceneId
                    }));
                }
                return RunThenNext(_watch, _analytics, next, ctx);
            }
            else
            {
                return next(ctx);
            }
        }

        public Task RunFF(ApiCallContext<Packet<IScenePeerClient>> ctx, Func<ApiCallContext<Packet<IScenePeerClient>>, Task> next)
        {
            if (_config.Value.EnableApiInstrumentation)
            {
                static async Task RunThenNext(Stopwatch watch, IAnalyticsService analytics, Func<ApiCallContext<Packet<IScenePeerClient>>, Task> next, ApiCallContext<Packet<IScenePeerClient>> ctx)
                {
                    var start = watch.ElapsedMilliseconds;
                    await next(ctx);
                    analytics.Push("api", "ff.cs", JObject.FromObject(new
                    {
                        type = "FireForget",
                        scope = "ClientServer",
                        inputSize = ctx.Context.Stream.Length,
                        route = ctx.Route,
                        duration = watch.ElapsedMilliseconds - start,
                        SessionId = ctx.Context.Connection.SessionId.ToString()
                    }));
                }
                return RunThenNext(_watch, _analytics, next, ctx);
            }
            else
            {
                return next(ctx);
            }
        }

        public Task OnConnected(IScenePeerClient client)
        {
            if(_config.Value.EnableSceneEventsInstrumentation)
            {
                _analytics.Push("api", "scene", JObject.FromObject(new
                {
                    type = "scene.connect",
                    
                }));
            }
            return Task.CompletedTask;
        }

        public Task OnDisconnected(IScenePeerClient client)
        {
            if (_config.Value.EnableSceneEventsInstrumentation)
            {
                _analytics.Push("api", "ff.cs", JObject.FromObject(new
                {
                    type = "scene.disconnect",
                }));
            }
            return Task.CompletedTask;
        }

    
        public async Task RunS2S(ApiCallContext<IS2SRequestContext> ctx, Func<ApiCallContext<IS2SRequestContext>, Task> next)
        {
            if (_config.Value.EnableS2SInstrumentation)
            {
                var start = _watch.ElapsedMilliseconds;
                await next(ctx);
                _analytics.Push("api", "ff.req", JObject.FromObject(new
                {
                    type = "Request",
                    scope = "S2S",
                    inputSize = -1,
                    route = ctx.Route,
                    duration = _watch.ElapsedMilliseconds - start,
                    SessionId = ctx.Context.Origin
                }));

            }
            else
            {
                await next(ctx);
            }
        }
    }
}

