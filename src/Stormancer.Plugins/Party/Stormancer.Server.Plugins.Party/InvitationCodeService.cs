using Stormancer.Core;
using Stormancer.Server.Plugins.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{

    class InvitationCodeService
    {
        private const string CODE_CHARACTERS = "abcdefghjkmnopqrstuvwxyzABCDEFGHIJKLMNPQRSTUVWXYZ0123456789";
        private const int CODE_LENGTH = 6;
        private static Random random = new Random();
        private class InvitationCodeState
        {
            internal InvitationCodeState(ISceneHost scene)
            {
                Scene = scene;
            }
            public ISceneHost Scene { get; }
            public bool Active { get; set; }
            public Guid Uid { get; } = Guid.NewGuid();
        }
        private object syncRoot = new object();
        private Dictionary<string, InvitationCodeState> codes = new Dictionary<string, InvitationCodeState>();
        private readonly IHost host;
        private readonly ISerializer serializer;
        private readonly ManagementClientProvider management;

        public InvitationCodeService(IHost host, ISerializer serializer, ManagementClientProvider management)
        {
            this.host = host;
            this.serializer = serializer;
            this.management = management;
        }

        public void CancelCode(ISceneHost scene)
        {
            var sceneId = scene.Id;
            lock (syncRoot)
            {
                var kvp = codes.FirstOrDefault(kvp => kvp.Value.Scene.Id == sceneId);
                if (kvp.Key != null)
                {
                    codes.Remove(kvp.Key);
                }
            }
        }

        public async Task<string> CreateCode(ISceneHost scene,CancellationToken cancellationToken)
        {
            string? code;
            do
            {
                code = await TryCreateCode(scene, cancellationToken);
            }
            while (code == null);

            return code;
        }

        public async Task<string?> CreateConnectionTokenFromInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken)
        {
            var sceneId = await GetSceneIdForInvitationCode(invitationCode, cancellationToken);
            if(sceneId != null)
            {
                return await management.CreateConnectionToken(sceneId);
            }
            else
            {
                return null;
            }

        }

        private async Task<string?> GetSceneIdForInvitationCode(string invitationCode, CancellationToken cancellationToken)
        {
            using var request = await host.StartAppFunctionRequest("party.getSceneIdForInvitationCode", cancellationToken);
            await serializer.SerializeAsync(invitationCode, request.Input, cancellationToken);

            string? sceneId = null;
            await foreach (var result in request.Results)
            {
                if (result.IsSuccess)
                {
                    
                    var r = await serializer.DeserializeAsync<string?>(result.Output, cancellationToken);
                    if(r!=null && sceneId == null)
                    {
                        sceneId = r;
                    }
                }
            }
            return sceneId;
        }

        private async Task<string?> TryCreateCode(ISceneHost scene, CancellationToken cancellationToken)
        {
            string code;
            InvitationCodeState state;
            lock (syncRoot)
            {
                do
                {
                    code = GenerateCode();
                }
                while (codes.ContainsKey(code));
                state = new InvitationCodeState(scene);
                codes.Add(code, state);
            }

            using var request = await host.StartAppFunctionRequest("party.isInvitationCodeAvailable", cancellationToken);
            await serializer.SerializeAsync(code, request.Input, cancellationToken);
            await serializer.SerializeAsync(state.Uid.ToByteArray(), request.Input, cancellationToken);

            bool found = false;
            await foreach (var result in request.Results)
            {
                if (result.IsSuccess)
                {
                    found |= await serializer.DeserializeAsync<bool>(result.Output, cancellationToken);
                }
            }

            if (found)
            {
                lock (syncRoot)
                {
                    codes.Remove(code);
                }
                return null;

            }
            else
            {
                state.Active = true;
                return code;
            }

        }

        internal void Initialize()
        {
            host.RegisterAppFunction("party.getSceneIdForInvitationCode",async ctx=> {
                var code = await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None);

                string? sceneId = null;
                lock(syncRoot)
                {
                    if(codes.TryGetValue(code,out var state) && state.Active)
                    {
                        sceneId = state.Scene.FullId; 
                    }
                }

                await serializer.SerializeAsync(sceneId, ctx.Output, CancellationToken.None);
            });
            host.RegisterAppFunction("party.isInvitationCodeAvailable",async ctx=> {

                var code = await serializer.DeserializeAsync<string>(ctx.Input,CancellationToken.None);
                var uid = new Guid(await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None));
                bool found;
                lock(syncRoot)
                {
                    if(!codes.TryGetValue(code,out var state))
                    {
                        found = false;
                    }
                    else
                    {
                        if(state.Uid == uid)// It's the entry we just created. So no concurrent entry was found.
                        {
                            found = false;
                        }
                        else//The uid of the found entry does not match the one we juste created, we have a code duplicate.
                        {
                            found = true;
                        }
                    }
                }

                await serializer.SerializeAsync(found, ctx.Output,CancellationToken.None);

            });
        }

        private string GenerateCode()
        {
            var builder = new StringBuilder(CODE_LENGTH);
            for (int i = 0; i < CODE_LENGTH; i++)
            {
                builder.Append(CODE_CHARACTERS[random.Next(0, CODE_CHARACTERS.Length)]);
            }
            return builder.ToString();
        }
    }
}
