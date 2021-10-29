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
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Provides server pool providers.
    /// </summary>
    public interface IServerPoolProvider
    {
        /// <summary>
        /// Tries creating a server pool provider.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="config"></param>
        /// <param name="pool"></param>
        /// <returns></returns>
        bool TryCreate(string id, JObject config,out IServerPool pool);
    }

    /// <summary>
    /// A server instance currently running in a pool.
    /// </summary>
    public class Server : IDisposable
    {
        public string Id { get; internal set; }
        public GameServer GameServer { get; internal set; }
        public DateTime CreatedOn { get; internal set; }
        public IScenePeerClient Peer { get; internal set; }
        public TaskCompletionSource<GameServerStartupParameters> RunTcs { get; internal set; }


        /// <summary>
        /// Shutsdown a server.
        /// </summary>
        /// <returns></returns>
        Task Shutdown()
        {
            return Peer.Send("ServerPool.Shutdown", s => { }, Core.PacketPriority.MEDIUM_PRIORITY, Core.PacketReliability.RELIABLE);
        }

        public void Dispose()
        {
        }
    }

    public interface IServerPool: IDisposable
    {
        string Id { get; }
        Task<Server> GetServer(string gameSessionId, GameSessionConfiguration gameSessionConfig);

        void UpdateConfiguration(JObject config);

        int ServersReady { get; }
        int ServersStarting { get; }
        int ServersRunning { get; }
        int TotalServersInPool { get; }
        int PendingServerRequests { get; }
        bool CanAcceptRequest { get; }
        int MaxServersInPool { get; }
        int MinServerReady { get; }

        Task<GameServerStartupParameters> SetReady(string gameId, IScenePeerClient client);
        Task SetShutdown(string gameId);
    }
}
