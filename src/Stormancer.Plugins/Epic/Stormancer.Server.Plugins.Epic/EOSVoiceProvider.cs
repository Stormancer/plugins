using Stormancer.Server.Plugins.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    internal class EOSVoiceProvider : IVoiceChatProvider
    {
        public string Type => "eos";

        public Task<VoiceChatTicket> AddUserToRoom(string roomId, SessionId sessionId)
        {
            throw new NotImplementedException();
        }

        public Task RemoveUserFromRoom(string roomId, SessionId sessionId)
        {
            throw new NotImplementedException();
        }
    }
}
