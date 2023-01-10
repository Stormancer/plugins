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

using Stormancer.Core;
using Stormancer.Server.Plugins.Management;
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
        private readonly ManagementClientProvider _management;
        private readonly ISceneHost _scene;

        public PartyManagementService(
            InvitationCodeService invitationCodes,
            ManagementClientProvider management,
            ISceneHost scene,
            IServiceLocator serviceLocator
            )
        {
            _serviceLocator = serviceLocator;
            this.invitationCodes = invitationCodes;
            _management = management;
            _scene = scene;
        }

        public async Task<string> CreateParty(PartyRequestDto partyRequest, string leaderUserId)
        {
            var partyId = string.IsNullOrWhiteSpace(partyRequest.PlatformSessionId) ? Guid.NewGuid().ToString() : partyRequest.PlatformSessionId;
            var sceneUri = await _serviceLocator.GetSceneId(PartyPlugin.PARTY_SERVICEID, partyId);

            if (string.IsNullOrEmpty(partyRequest.GameFinderName))
            {
                throw new ArgumentException("partyRequest.GameFinderName", "GameFinderName cannot be empty");
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

            await _management.CreateScene(
                sceneUri,
                PartyPlugin.PARTY_SCENE_TYPE,
                false,
                false,
                metadata
            );

            return await _management.CreateConnectionToken(sceneUri,partyRequest.UserData, "party/userdata");
        }


        public Task<string?> CreateConnectionTokenFromInvitationCodeAsync(string invitationCode,byte[] userData, CancellationToken cancellationToken)
        {
            return invitationCodes.CreateConnectionTokenFromInvitationCodeAsync(invitationCode,userData, cancellationToken);
        }

        public async Task<string?> CreateConnectionTokenFromPartyId(string partyId, byte[] userData, CancellationToken cancellationToken)
        {
            var sceneUri = await _serviceLocator.GetSceneId(PartyPlugin.PARTY_SERVICEID, partyId);

            if(sceneUri == null)
            {
                return null;
            }
            return await _management.CreateConnectionToken(sceneUri, userData, "party/userdata");
        }
    }
}
