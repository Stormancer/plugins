using Newtonsoft.Json;
using Org.BouncyCastle.Bcpg;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    /// <summary>
    /// Epic Platform service
    /// </summary>
    public interface IEOSService
    {
        /// <summary>
        /// Is Epic the main auth of this session?
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public bool IsEpicAccount(Session session); 

        /// <summary>
        /// Get Epic accounts.
        /// </summary>
        /// <param name="accountIds"></param>
        /// <returns></returns>
        public Task<Dictionary<string, Account>> GetAccounts(IEnumerable<string> accountIds);

        /// <summary>
        /// Get eos product ids from external account ids.
        /// </summary>
        /// <param name="externalAccountIds"></param>
        /// <param name="identityProviderId"></param>
        /// <param name="environment"></param>
        /// <returns></returns>
        Task<Dictionary<string, string?>> GetExternalAccounts(IEnumerable<string> externalAccountIds, string identityProviderId = "epicgames", string? environment = null);


        /// <summary>
        /// Creates a token used to connect to a voice room.
        /// </summary>
        /// <remarks>
        /// 
        /// Documentation on https://dev.epicgames.com/docs/web-api-ref/voice-web-api#creating-room-tokens
        /// </remarks>
        /// <returns></returns>
        Task<EosVoiceRoomToken> CreateVoiceRoomToken(string voiceRoomId, IEnumerable<EosRoomParticipantRequest> participants);
    
        /// <summary>
        /// Removes a participan from a voice room.
        /// </summary>
        /// <param name="voiceRoomId"></param>
        /// <param name="userProductId"></param>
        /// <returns></returns>
        Task RemoveVoiceRoomParticipant(string voiceRoomId,string userProductId);
    }

    /// <summary>
    /// Request for a player to participate in a voice room.
    /// </summary>
    public class EosRoomParticipantRequest
    {
        /// <summary>
        /// Product user id of the participant.
        /// </summary>
        public required string ProductUserId { get; set; }

        ///<summary>
        ///Optional ip address of the participant.
        /// </summary>
        /// <remarks>
        /// IP address of the player (also known as the "participant"). 
        /// The service uses this data to select a server for the voice session 
        /// that is close to the player's location. If there is no IP address 
        /// provided, the service chooses a server in a default location. This 
        /// might result in a poor player experience: the server might not be 
        /// close to the player's location which might result in the player 
        /// experiencing high network latency during the voice session.
        /// </remarks>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Initial mute status of the participant.
        /// </summary>
        public bool HardMuted { get; set; }
    }
    /// <summary>
    /// Participant to a voice room session.
    /// </summary>
    public class EosVoiceRoomParticipantInfo
    {
        /// <summary>
        /// The EOS ProductUserId of the participant.
        /// </summary>
        [DataMember(Name ="puid")]
        public required string ProductUserId { get; set; }

        /// <summary>
        /// The room token for that ProductUserId used to join the media server.
        /// </summary>
        public required string Token { get; set; }

        /// <summary>
        /// Mute status of the participant.
        /// </summary>
        public bool HardMuted { get; set; }
    }

    /// <summary>
    /// Result of <see cref="IEOSService.CreateVoiceRoomToken"/>
    /// </summary>
    public class EosVoiceRoomToken
    {
        /// <summary>
        /// Id of the room
        /// </summary>
        public required string RoomId { get; set; }

        /// <summary>
        /// The URL of the media server to join for the voice session
        /// </summary>
        public required string ClientBaseUri { get; set; }

        /// <summary>
        /// The deployment id associated with the request.
        /// </summary>
        public required string DeploymentId { get; set; }

        /// <summary>
        /// List of participants.
        /// </summary>
        public required EosVoiceRoomParticipantInfo[] Participants { get; set; }
    }
}
