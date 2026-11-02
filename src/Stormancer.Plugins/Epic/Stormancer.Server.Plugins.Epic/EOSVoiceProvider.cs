using Stormancer.Server.Plugins.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    internal class EosVoiceRoomsState
    {

    }
    internal class EOSVoiceProvider : IVoiceChatProvider
    {
        private readonly EOSService _eos;
        private readonly EosVoiceRoomsState _state;

        public string Type => "eos";

        public EOSVoiceProvider(EOSService eos, EosVoiceRoomsState state)
        {
            _eos = eos;
            _state = state;
        }

        public Task<VoiceChatTicket> AddUserToRoom(string roomId, IScenePeerClient peer)
        {
            peer.
            _eos.CreateVoiceRoomToken(roomId, [new EosVoiceRoomParticipantRequest { ClientIp ="", ProductUserId =  }]);
        }

        public Task RemoveUserFromRoom(string roomId, SessionId sessionId)
        {
            throw new NotImplementedException();
        }
    }
}
