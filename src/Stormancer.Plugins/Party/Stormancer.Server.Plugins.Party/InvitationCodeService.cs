using Stormancer.Abstractions.Server.Components;
using Stormancer.Core;
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
        private readonly IClusterSerializer serializer;
        private readonly IScenesManager management;
        private readonly PartyConfigurationService partyConfiguration;

        public InvitationCodeService(IHost host, IClusterSerializer serializer, IScenesManager management, PartyConfigurationService partyConfiguration)
        {
            this.host = host;
            this.serializer = serializer;
            this.management = management;
            this.partyConfiguration = partyConfiguration;
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

        public async Task<string> CreateCode(ISceneHost scene, CancellationToken cancellationToken)
        {
            string? code;
            do
            {
                code = await TryCreateCode(scene, cancellationToken);
            }
            while (code == null);

            return code;
        }

        public async Task<string?> CreateConnectionTokenFromInvitationCodeAsync(string invitationCode, byte[] userData, CancellationToken cancellationToken)
        {
            var sceneId = await GetSceneIdForInvitationCode(invitationCode, cancellationToken);
            if (sceneId != null)
            {
                return await management.CreateConnectionTokenAsync(sceneId, userData, "party/userdata",3,cancellationToken);
            }
            else
            {
                return null;
            }

        }

        private async Task<string?> GetSceneIdForInvitationCode(string invitationCode, CancellationToken cancellationToken)
        {
            using var request = await host.CreateAppFunctionRequest("party.getSceneIdForInvitationCode", cancellationToken);
            serializer.Serialize(request.Input, invitationCode);
            request.Input.Complete();
            request.Send();
            string? sceneId = null;
            await foreach (var result in request.Results)
            {
                if (result.IsSuccess)
                {

                    var r = await serializer.DeserializeAsync<string?>(result.Output, cancellationToken);
                    result.Output.Complete();
                    if (r != null && sceneId == null)
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
                    code = GenerateCode(scene);
                }
                while (codes.ContainsKey(code));
                state = new InvitationCodeState(scene);
                codes.Add(code, state);
            }

            using var request = await host.CreateAppFunctionRequest("party.isInvitationCodeAvailable", cancellationToken);
            serializer.Serialize(request.Input, code);
            serializer.Serialize(request.Input, state.Uid.ToByteArray());
            request.Input.Complete();
            request.Send();
            bool found = false;
            await foreach (var result in request.Results)
            {
                if (result.IsSuccess)
                {
                    found |= await serializer.DeserializeAsync<bool>(result.Output, cancellationToken);
                    result.Output.Complete();
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
            host.RegisterAppFunction("party.getSceneIdForInvitationCode", async ctx =>
            {
                var code = await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None);

                string? sceneId = null;
                lock (syncRoot)
                {
                    if (codes.TryGetValue(code, out var state) && state.Active)
                    {
                        sceneId = state.Scene.Id;
                    }
                }

                serializer.Serialize(ctx.Output, sceneId);
            });
            host.RegisterAppFunction("party.isInvitationCodeAvailable", async ctx =>
            {

                var code = await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None);
                var uid = new Guid(await serializer.DeserializeAsync<byte[]>(ctx.Input, CancellationToken.None));
                bool found;
                lock (syncRoot)
                {
                    if (!codes.TryGetValue(code, out var state))
                    {
                        found = false;
                    }
                    else
                    {
                        if (state.Uid == uid)// It's the entry we just created. So no concurrent entry was found.
                        {
                            found = false;
                        }
                        else//The uid of the found entry does not match the one we juste created, we have a code duplicate.
                        {
                            found = true;
                        }
                    }
                }

                serializer.Serialize(ctx.Output, found);

            });
        }

        private string GenerateCode(ISceneHost scene)
        {
            var codeLength = partyConfiguration.GetInvitationCodeLength(scene);
            var codeCharacters = partyConfiguration.GetAuthorizedInvitationCodeCharacters(scene);
            var builder = new StringBuilder(codeLength);
            for (int i = 0; i < codeLength; i++)
            {
                builder.Append(codeCharacters[random.Next(0, codeCharacters.Length)]);
            }
            return builder.ToString();
        }
    }
}
