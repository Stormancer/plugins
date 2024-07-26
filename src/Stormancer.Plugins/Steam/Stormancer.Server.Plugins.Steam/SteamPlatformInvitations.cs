using MessagePack;
using Stormancer.Core;
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Party.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    [MessagePackObject]
    public class InviteUserToLobbyArgs
    {
        [Key(0)]
        public required ulong SteamUserId { get; set; }

        [Key(1)]
        public required ulong SteamLobbyId { get; set; }
    }
    internal class SteamPlatformInvitationsHandler : IPartyPlatformSupport
    {
        private readonly ISceneHost _scene;
        private readonly IFriendsService _friends;

        public SteamPlatformInvitationsHandler(ISceneHost scene, IFriendsService friends)
        {
            _scene = scene;
            _friends = friends;
        }

        public string PlatformName => "steam";

        public bool CanHandle(InvitationContext ctx)
        {
            //Sender must be connected with Steam.
            if (ctx.Sender.platformId.Platform != SteamConstants.PLATFORM_NAME)
            {
                return false;
            }

            //If recipient is a steam user, it's ok.
            if (ctx.RecipientUserId.Platform == SteamConstants.PLATFORM_NAME)
            {
                return true;
            }

            
           
            return false;


        }

        public async Task<bool> SendInvitation(InvitationContext ctx)
        {
            if (ctx.Sender.platformId.Platform != SteamConstants.PLATFORM_NAME)
            {
                return false;
            }

            ulong steamId;
            if (ctx.RecipientUserId.Platform == SteamConstants.PLATFORM_NAME)
            {
                steamId = ulong.Parse(ctx.RecipientUserId.PlatformUserId);
            }
            else if (ctx.RecipientUser != null && ctx.RecipientUser.TryGetSteamId(out steamId))
            {

            }
            else
            {
                return false;
            }
            if (!_scene.TryGetPeer(ctx.Sender.SessionId, out var peer))
            {
                return false;
            }
            if (ctx.Party.ServerData.TryGetValue(SteamPartyEventHandler.PartyLobbyKey, out var data) && data is SteamPartyData steamPartyData && steamPartyData.SteamIDLobby != null)
            {
                var result = await peer.RpcTask<InviteUserToLobbyArgs,VoidSteamResult>("Steam.Invite", new InviteUserToLobbyArgs
                {
                    SteamUserId = steamId,
                    SteamLobbyId = steamPartyData.SteamIDLobby.Value
                });

                return result.Success;
            }
            else
            {
                return false;
            }

        }
    }
}
