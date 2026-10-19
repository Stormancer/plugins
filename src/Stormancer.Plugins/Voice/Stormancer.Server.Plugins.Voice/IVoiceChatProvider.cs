using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Voice
{
    public interface IVoiceChatProvider
    {
        string Type { get; }

        Task<VoiceChatTicket> AddUserToRoom(string roomId, SessionId sessionId);
        Task RemoveUserFromRoom(string roomId, SessionId sessionId);
    }
}
