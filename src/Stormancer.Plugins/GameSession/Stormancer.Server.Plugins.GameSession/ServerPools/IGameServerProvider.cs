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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// A game server instance.
    /// </summary>
    public class GameServerInstance
    {
        /// <summary>
        /// Gets or sets the id of the server.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets an action executed when the game server is closed.
        /// </summary>
        public Action? OnClosed { get; set; }
    }

    /// <summary>
    /// The result of a call to <see cref="IGameServerProvider.TryStartServer(string, string, JObject, IEnumerable{string}, CancellationToken)"/>.
    /// </summary>
    public class StartGameServerResult
    {
        /// <summary>
        /// Creates a new <see cref="StartGameServerResult"/> object.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="instance"></param>
        /// <param name="context"></param>
        public StartGameServerResult(bool success, GameServerInstance? instance, object? context )
        {
            Success = success;
            Instance = instance;
            Context = context;
        }

        /// <summary>
        /// Gets a boolean indicating whether  the request was successful.
        /// </summary>
        [MemberNotNullWhen(true,"Instance")]
        public bool Success { get; }

        /// <summary>
        /// Gets the <see cref="GameServerInstance"/> object that contains details about the started game server.
        /// </summary>
        public GameServerInstance? Instance { get; }

        /// <summary>
        /// Gets an optional context passed to <see cref="IGameServerProvider.StopServer(string, object?)"/>.
        /// </summary>
        public object? Context { get; }

        /// <summary>
        /// Gets or sets the region the game server was created in.
        /// </summary>
        public string? Region { get; set; }
    }


    /// <summary>
    /// Provides a way to host game servers.
    /// </summary>
    public interface IGameServerProvider
    {
        /// <summary>
        /// Gets the id of the provider.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Tries to start a server on a region.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="authToken"></param>
        /// <param name="config"></param>
        /// <param name="preferredRegions"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<StartGameServerResult> TryStartServer(string id,string authToken, JObject config,IEnumerable<string> preferredRegions, CancellationToken ct);

        /// <summary>
        /// Stops a server.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        Task StopServer(string id, object? context);


        /// <summary>
        /// Queries logs of the server.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="since"></param>
        /// <param name="until"></param>
        /// <param name="size"></param>
        /// <param name="follow"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<string> QueryLogsAsync(string id, DateTime? since, DateTime? until, uint size, bool follow, CancellationToken cancellationToken);
        
        /// <summary>
        /// Extend the life duration of the server.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        Task<bool> KeepServerAliveAsync(string gameSessionId, object? context);
    }
}
