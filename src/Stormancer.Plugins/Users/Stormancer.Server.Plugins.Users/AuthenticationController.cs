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

using Stormancer.Server.Plugins.API;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Diagnostics;
using System.Reactive.Linq;

namespace Stormancer.Server.Plugins.Users
{
    internal sealed class AuthenticationController : ControllerBase
    {

        private readonly IAuthenticationService _auth;
        private readonly IUserSessions sessions;
        private readonly RpcService rpc;
        private readonly ISceneHost _scene;

        public AuthenticationController(IAuthenticationService auth, IUserSessions sessions, RpcService rpc, ISceneHost scene)
        {

            _auth = auth;
            this.sessions = sessions;
            this.rpc = rpc;
            _scene = scene;
        }
        protected override async Task OnDisconnected(DisconnectedArgs args)
        {
            var s = sessions as UserSessions;
            if (s != null)
            {
                DisconnectionReason reason;
                switch (args.Reason)
                {
                    case "CONNECTION_LOST":
                        reason = DisconnectionReason.ConnectionLoss;
                        break;
                    case "CLIENT_REQUEST":
                        reason = DisconnectionReason.ClientDisconnected;
                        break;
                    default:
                        reason = DisconnectionReason.ServerRequest;
                        break;
                }

                await s.LogOut(args.Peer.SessionId, reason);
            }

        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Dictionary<string, string> GetMetadata()
        {
            return _auth.GetMetadata();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Register(AuthParameters ctx)
        {
            await _auth.SetupAuth(ctx);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<LoginResult> Login(AuthParameters parameters, RequestContext<IScenePeerClient> ctx)
        {
            return await _auth.Login(parameters, ctx.RemotePeer, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task RememberDeviceForTwoFactor(RememberDeviceParameters parameters, RequestContext<IScenePeerClient> ctx)
        {
            await _auth.RememberDeviceFor2fa(parameters, ctx.RemotePeer, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<Dictionary<string, string>> GetStatus(RequestContext<IScenePeerClient> ctx) => _auth.GetStatus(ctx.RemotePeer, ctx.CancellationToken);

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Unlink(string type, RequestContext<IScenePeerClient> ctx)
        {
            var user = await sessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);
            if (user == null)
            {
                throw new ClientException("NotFound");
            }
            await _auth.Unlink(user, type);
        }
        [Api(ApiAccess.Public, ApiType.Rpc, Route = "sendRequest")]
        public async Task SendRequest(string userId, RequestContext<IScenePeerClient> ctx)
        {
            var sessionIds = await sessions.GetPeers(userId, ctx.CancellationToken);
            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionIds.FirstOrDefault());
            var sender = await sessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (peer == null)
            {
                throw new ClientException($"userDisconnected?id={userId}");
            }
            var tcs = new TaskCompletionSource<bool>();


            var disposable = rpc.Rpc("sendRequest", peer, s =>
            {
                peer.Serializer().Serialize(sender.Id, s);
                ctx.InputStream.CopyTo(s);
            }, PacketPriority.MEDIUM_PRIORITY)
                .Select(packet => Observable.FromAsync(async () => await ctx.SendValue(s => packet.Stream.CopyTo(s))))
                .Concat()
                .Subscribe(
                    _ => { },
                    (error) => tcs.SetException(error),
                    () => tcs.SetResult(true)
                );

            ctx.CancellationToken.Register(() =>
            {
                disposable.Dispose();
            });
            try
            {
                await tcs.Task;

            }
            catch (TaskCanceledException ex)
            {
                if (ex.Message == "Peer disconnected")
                {
                    throw new ClientException($"userDisconnected?id={userId}");
                }
                else
                {
                    throw new ClientException("requestCanceled");
                }
            }
        }
    }
}

