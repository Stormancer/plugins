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

using Stormancer.Abstractions.Server.Components;
using Stormancer.Core;
using Stormancer.Management;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.PartyManagement
{
    //Todo jojo need cleanup if the aren't complete session creation
    class PartyManagementService : IPartyManagementService
    {
        public const string PROTOCOL_VERSION = "2020-05-20.1";

        private readonly IServiceLocator _serviceLocator;
        private readonly InvitationCodeService invitationCodes;

        // Services
        private readonly IScenesManager _management;
        private readonly IEnvironment _env;
        private readonly ISceneHost _scene;

        public PartyManagementService(
            InvitationCodeService invitationCodes,
            IScenesManager management,
            IEnvironment env,
            ISceneHost scene,
            IServiceLocator serviceLocator
            )
        {
            _serviceLocator = serviceLocator;
            this.invitationCodes = invitationCodes;
            _management = management;
            _env = env;
            _scene = scene;
        }

        public async Task<string> CreateParty(PartyRequestDto partyRequest, string leaderUserId)
        {
            if (string.IsNullOrEmpty(partyRequest.GameFinderName))
            {
                throw new ArgumentException("partyRequest.GameFinderName", "GameFinderName cannot be empty");
            }

            var partyId = string.IsNullOrWhiteSpace(partyRequest.PlatformSessionId) ? Guid.NewGuid().ToString() : partyRequest.PlatformSessionId;
            var sceneUri = await _serviceLocator.GetSceneId(PartyPlugin.PARTY_SERVICEID, partyId);
            if (string.IsNullOrEmpty(sceneUri))
            {
                throw new InvalidOperationException("Failed to generate scene id for party.");
            }


            var metadata = Newtonsoft.Json.Linq.JObject.FromObject(new
            {
                party = new
                {
                    PartyId = partyId,
                    PartyLeaderId = leaderUserId,
                    partyRequest.CustomData,
                    partyRequest.GameFinderName,
                    partyRequest.ServerSettings,
                    partyRequest.IsPublic,
                    partyRequest.OnlyLeaderCanInvite,
                    partyRequest.IsJoinable
                }
            });
            var appInfos = await _env.GetApplicationInfos();
            await _management.CreateOrUpdateSceneAsync(new Platform.Core.Models.SceneDefinition
            {
                AccountId = appInfos.AccountId,
                Application = appInfos.ApplicationName,
                Id = sceneUri,
                SceneType = PartyPlugin.PARTY_SCENE_TYPE,
                Metadata = metadata.ToDictionary(),
                PartitioningPolicy = Stormancer.Server.Cluster.Constants.PARTITIONING_POLICY_HASH,
                ShardGroup = Cluster.Constants.SHARDGROUP_DEFAULT,

            });

            return await _management.CreateConnectionTokenAsync(sceneUri, partyRequest.UserData, "party/userdata");
        }


        public Task<string?> CreateConnectionTokenFromInvitationCodeAsync(string invitationCode, Memory<byte> userData, CancellationToken cancellationToken)
        {
            return invitationCodes.CreateConnectionTokenFromInvitationCodeAsync(invitationCode, userData.ToArray(), cancellationToken);
        }

        public async Task<Result<string, string>> CreateConnectionTokenFromPartyId(string partyId, Memory<byte> userData, CancellationToken cancellationToken)
        {
            string? sceneUri;
            //HACK Windjammers 2. REMOVE
            if (!partyId.StartsWith("party-"))
            {
                sceneUri = await _serviceLocator.GetSceneId(PartyPlugin.PARTY_SERVICEID, partyId);

                if (sceneUri == null)
                {
                    return Result<string, string>.Failed("notFound");
                }
            }
            else
            {
                sceneUri = partyId;
            }

            try
            {
                var result = await _management.CreateConnectionTokenAsync(sceneUri, userData.ToArray(), "party/userdata", 3, cancellationToken);

                return Result<string, string>.Succeeded(result);

            }
            catch (Exception ex)
            {
                return Result<string, string>.Failed(ex.Message);
            }
        }
    }




}
