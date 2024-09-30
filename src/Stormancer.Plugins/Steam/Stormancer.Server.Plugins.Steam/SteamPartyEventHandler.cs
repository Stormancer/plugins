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

using Org.BouncyCastle.Asn1.X509;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    internal class SteamUserData
    {
        public SessionId SessionId { get; set; }

        public ulong SteamId { get; set; }
    }

    internal class SteamPartyData
    {
        public TaskQueue TaskQueue { get; } = new();

        public ulong? SteamIDLobby { get; set; }
        public SessionId CurrentLeaderSessionId { get; set; }
        public uint AppId { get; set; }
        public int NumMembers => UserData.Count;

        // UserData[SessionId] => SteamUserData
        public ConcurrentDictionary<SessionId, SteamUserData> UserData { get; set; } = new();



        public bool IsJoinable { get; set; }
    }

    internal class SteamLobby
    {
        public SessionId CurrentLeaderSessionId { get; set; }
        public ulong SteamIDLobby { get; set; }

        public bool IsJoinable { get; set; } = true;
    }

    /// <summary>
    /// Exception thrown when a leader cannot be found for the lobby.
    /// </summary>
    public class SteamLobbyLeaderNotFoundException : Exception
    {

    }
    /// <summary>
    /// Steam Party Event Handler.
    /// </summary>
    [Priority(int.MinValue)]
    internal class SteamPartyEventHandler : IPartyEventHandler
    {
        private const string LobbyPrefix = "Party-";
        public const string PartyLobbyKey = "steam.lobby";


        private readonly ISteamService _steamService;
        private readonly ILogger _logger;
        private readonly ISceneHost _scene;


        /// <summary>
        /// Steam Party Event Handler contructor.
        /// </summary>
        /// <param name="steam"></param>
        /// <param name="logger"></param>
        /// <param name="scene"></param>
        public SteamPartyEventHandler(
            ISteamService steam,
            ILogger logger,
            ISceneHost scene
        )
        {
            _steamService = steam;
            _logger = logger;
            _scene = scene;
        }



        /// <summary>
        /// Steam behavior on party joined.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task OnPreJoined(PreJoinedPartyContext ctx)
        {

            if (ctx.Session.platformId.Platform != SteamConstants.PLATFORM_NAME)
            {
                return;
            }
            if (!ctx.Session.TryGetSteamAppId(out var steamAppId) || ctx.Session.User == null || !ctx.Session.User.TryGetSteamId(out var steamId))
            {
                return;
            }

            if (!ctx.Party.Settings.ServerSettings.ShouldCreateSteamLobby() ?? false)
            {
                return;
            }


            var data = (SteamPartyData)ctx.Party.ServerData.GetOrAdd(PartyLobbyKey, new SteamPartyData());

            if (!data.SteamIDLobby.HasValue)
            {


                var result = await CreateSteamLobbyAsync(ctx.Peer, ctx.Session, ctx.Party, CancellationToken.None);

                if (!result.Success)
                {

                    if (ctx.Party.Settings.ServerSettings.DoNotJoinIfSteamLobbyCreationFailed())
                    {
                        ctx.ErrorId = result.ErrorId;

                    }
                    return;
                }
                var steamIDLobby = result.SteamLobbyId;
                data.SteamIDLobby = steamIDLobby;
                var partySettingsDto = new PartySettingsDto(ctx.Party.State);
                if (partySettingsDto.PublicServerData == null)
                {
                    partySettingsDto.PublicServerData = new();
                }
                partySettingsDto.PublicServerData["SteamIDLobby"] = steamIDLobby.ToString();
                _ = ctx.Party.UpdateSettings(partySettingsDto, CancellationToken.None);

                data.UserData[ctx.Session.SessionId] = new SteamUserData { SessionId = ctx.Session.SessionId, SteamId = steamId };
                data.AppId = steamAppId;

                data.IsJoinable = ctx.Party.Settings.ServerSettings.ShouldSyncJoinable() ? ctx.Party.Settings.IsJoinable : true;
                data.CurrentLeaderSessionId = ctx.Session.SessionId;
            }
            else
            {
                var result = await JoinSteamLobbyAsync(ctx.Peer, data.SteamIDLobby.Value, CancellationToken.None);

                if (!result.Success)
                {

                    if (ctx.Party.Settings.ServerSettings.DoNotJoinIfSteamLobbyCreationFailed())
                    {
                        ctx.ErrorId = result.ErrorId;

                    }
                    return;
                }
                data.UserData[ctx.Session.SessionId] = new SteamUserData { SessionId = ctx.Session.SessionId, SteamId = steamId };


            }

        }

        private async Task<CreateSteamLobbyResult> CreateSteamLobbyAsync(IScenePeerClient leaderPeer, Session leaderSession, IPartyService party, CancellationToken cancellationToken)
        {
            if (leaderSession.User == null)
            {
                throw new InvalidOperationException("The user must be authenticated with steam.");
            }

            var steamId = leaderSession.User.GetSteamId();
            var lobbyName = $"{LobbyPrefix}{party.Settings.PartyId}";


            var joinable = party.Settings.ServerSettings.ShouldSyncJoinable() ? party.Settings.IsJoinable : true;

            var maxMembers = party.Settings.ServerSettings.SteamMaxMembers() ?? 5;
            var lobbyType = party.Settings.ServerSettings.SteamLobbyType() ?? LobbyType.FriendsOnly;

            var partyDataBearerToken = await _steamService.CreatePartyDataBearerToken(party.Settings.PartyId, leaderSession.User.Id, (ulong)steamId);
            var createLobbyParameters = new CreateLobbyDto
            {
                LobbyType = lobbyType,
                MaxMembers = maxMembers,
                Joinable = joinable,
                Metadata = new Dictionary<string, string> { { "partyDataToken", partyDataBearerToken } }
            };

            var createSteamLobbyResult = await leaderPeer.RpcTask<CreateLobbyDto, CreateSteamLobbyResult>("Steam.CreateLobby", createLobbyParameters, cancellationToken);



            if (!createSteamLobbyResult.Success)
            {
                _logger.Log(LogLevel.Error, "SteamPartyEventHandler.OnJoining", "Steam lobby creation failed", new
                {
                    party.Settings.PartyId,
                    UserId = leaderSession.User.Id,
                    createSteamLobbyResult.ErrorId,
                    createSteamLobbyResult.ErrorDetails
                });

            }



            return createSteamLobbyResult;
        }

        private async Task<VoidSteamResult> JoinSteamLobbyAsync(IScenePeerClient target, ulong lobbyId, CancellationToken cancellationToken)
        {
            try
            {
                var joinLobbyParameter = new JoinLobbyArgs { SteamIDLobby = lobbyId };
                var joinSteamLobbyResult = await target.RpcTask<JoinLobbyArgs, VoidSteamResult>("Steam.JoinLobby", joinLobbyParameter, cancellationToken);


                if (!joinSteamLobbyResult.Success)
                {
                    _logger.Log(LogLevel.Error, "SteamPartyEventHandler.OnJoining", "Steam lobby join failed", new
                    {

                        joinSteamLobbyResult.ErrorId,
                        joinSteamLobbyResult.ErrorDetails
                    });


                }
                return joinSteamLobbyResult;
            }
            catch (OperationCanceledException)
            {
                return new VoidSteamResult { ErrorId = "canceled", Success = false };
            }
        }

        private async Task<VoidSteamResult> LeaveSteamLobbyAsync(IScenePeerClient target, ulong lobbyId, CancellationToken cancellationToken)
        {
            try
            {

                var joinLobbyParameter = new LeaveLobbyArgs { };
                var joinSteamLobbyResult = await target.RpcTask<LeaveLobbyArgs, VoidSteamResult>("Steam.LeaveLobby", joinLobbyParameter, cancellationToken, PacketPriority.MEDIUM_PRIORITY);
                if (!joinSteamLobbyResult.Success)
                {
                    _logger.Log(LogLevel.Error, "SteamPartyEventHandler.OnJoining", "Steam lobby join failed", new
                    {

                        joinSteamLobbyResult.ErrorId,
                        joinSteamLobbyResult.ErrorDetails
                    });


                }
                return joinSteamLobbyResult;
            }
            catch (InvalidOperationException ex) //don't bubble up exceptions occurring when the steam client can't process the request.
            {
                return new VoidSteamResult { Success = false, ErrorDetails = ex.Message, ErrorId = "communicationError" };
            }
        }

        private async Task<TResult> RunSteamCommand<TArgs, TResult>(ulong steamLobbyId, SteamPartyData data, Func<IScenePeerClient, TArgs, CancellationToken, Task<(TResult result, bool retry)>> func, TArgs args, int retries = 1, CancellationToken cancellationToken = default) where TResult : class
        {
            var tries = 0;
            TResult? lastResult = default;
            Exception? lastException = null;
            while (tries <= retries)
            {
                try
                {
                    var leader = await GetLobbyLeaderAsync(steamLobbyId, data, tries > 0, cancellationToken);
                    if (leader == null)
                    {
                        throw new SteamLobbyLeaderNotFoundException();
                    }
                    var (result, retry) = await func(leader, args, cancellationToken);

                    lastResult = result;
                    if (!retry)
                    {
                        return result;
                    }

                    await Task.Delay(500);
                }
                catch (SteamLobbyLeaderNotFoundException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(500);
                }


                tries++;
            }

            if (lastException != null)
            {
                throw lastException;
            }
            else
            {
                //If last exception is not filled, lastResult has been set at least once.
                return lastResult!;
            }

        }

        private async Task<IScenePeerClient?> GetLobbyLeaderAsync(ulong steamLobbyId, SteamPartyData data, bool forceRefresh, CancellationToken cancellationToken)
        {
            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == data.CurrentLeaderSessionId);
            if (peer == null || forceRefresh)
            {
                return await UpdateLobbyLeaderAsync(steamLobbyId, data, cancellationToken);
            }
            else
            {
                return peer;
            }
        }

        private async Task<IScenePeerClient?> UpdateLobbyLeaderAsync(ulong steamLobbyId, SteamPartyData data, CancellationToken cancellationToken)
        {
            var peer = _scene.RemotePeers.FirstOrDefault(p => data.UserData.ContainsKey(p.SessionId));
            if (peer == null)
            {
                return null;
            }
            else
            {
                var result = await peer.RpcTask<JoinLobbyArgs, GetLobbyLeaderSteamResult>("Steam.GetLobbyOwner", new JoinLobbyArgs { SteamIDLobby = steamLobbyId }, cancellationToken, Core.PacketPriority.MEDIUM_PRIORITY);

                if (!result.Success)
                {
                    _logger.Log(LogLevel.Error, "SteamPartyEventHandler.GetLobbyLeader", "get Steam lobby leader failed", new
                    {

                        result.ErrorId,
                        result.ErrorDetails
                    });
                    return null;

                }
                else
                {
                    var (leaderSessionId, leaderUserData) = data.UserData.FirstOrDefault(kvp => kvp.Value.SteamId == result.SteamId);
                    if (!leaderSessionId.IsEmpty())
                    {
                        data.CurrentLeaderSessionId = leaderSessionId;
                        return _scene.RemotePeers.FirstOrDefault(p => p.SessionId == leaderSessionId);

                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private async Task<VoidSteamResult> SetLobbyJoinableAsync(ulong steamLobbyId, bool joinable, SteamPartyData data, CancellationToken cancellationToken)
        {
            try
            {


                return await RunSteamCommand(steamLobbyId, data, static async (target, state, ct) =>
                {
                    var (joinable, steamLobbyId) = state;
                    if (target == null)
                    {
                        return (new VoidSteamResult { Success = false, ErrorDetails = "leader not found" },true);
                    }

                    var args = new UpdateLobbyJoinableArgs { Joinable = joinable, SteamIDLobby = steamLobbyId };
                    var result = await target.RpcTask<UpdateLobbyJoinableArgs, VoidSteamResult>("Steam.UpdateLobbyJoinable", args, ct, Core.PacketPriority.MEDIUM_PRIORITY);


                    return (result, !result.Success);

                }, (joinable, steamLobbyId), 1, cancellationToken);

            }
            catch (InvalidOperationException ex) //don't bubble up exceptions occurring when the steam client can't process the request.
            {
                return new VoidSteamResult { Success = false, ErrorDetails = ex.Message, ErrorId = "communicationError" };
            }
        }

        /// <summary>
        /// Steam behavior on party quit.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnQuit(QuitPartyContext ctx)
        {

            if (ctx.Party.ServerData.TryGetValue(PartyLobbyKey, out var dataObject))
            {
                var data = (SteamPartyData)dataObject;
                if (data != null && data.SteamIDLobby.HasValue)
                {
                    try
                    {
                        if (data.UserData.TryRemove(ctx.Args.Peer.SessionId, out _))
                        {
                            //Don't call LeaveSteamLobby because at this time, the peer already disconnected from the scene.
                            //await LeaveSteamLobbyAsync(ctx.Args.Peer, data.SteamIDLobby!.Value, CancellationToken.None);
                        }
                    }
                    finally
                    {
                        if (data.UserData.IsEmpty)
                        {
                            data.SteamIDLobby = null;
                        }
                    }
                }
            }

            return Task.CompletedTask;

        }

        /// <summary>
        /// Steam behavior on party settings update.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task OnUpdateSettings(PartySettingsUpdateCtx ctx)
        {
            if (ctx.Party.ServerData.TryGetValue(PartyLobbyKey, out var dataObject))
            {
                var data = (SteamPartyData)dataObject;
                if (data.SteamIDLobby.HasValue)
                {
                    if (ctx.Config.IsJoinable != data.IsJoinable && ctx.Party.Settings.ServerSettings.ShouldSyncJoinable())
                    {
                        var result = await SetLobbyJoinableAsync(data.SteamIDLobby.Value, ctx.Config.IsJoinable, data, CancellationToken.None);

                        if (result.Success)
                        {
                            data.IsJoinable = ctx.Config.IsJoinable;
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Steam behavior on party updating settings.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnUpdatingSettings(PartySettingsUpdateCtx ctx)
        {
            return Task.CompletedTask;
        }


    }
}
