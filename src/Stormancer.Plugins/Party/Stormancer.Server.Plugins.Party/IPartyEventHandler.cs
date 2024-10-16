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
using Stormancer.Server.PartyManagement;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.Party.Model;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Context passed to <see cref="IPartyEventHandler.OnCreatingParty(PartyCreationContext)"/>.
    /// </summary>
    public class PartyCreationContext
    {
        /// <summary>
        /// The party creation request parameters.
        /// </summary>
        public PartyRequestDto PartyRequest { get; }

        /// <summary>
        /// Gets or sets a value indicating whether party creation must succeed or fail.
        /// </summary>
        public bool Accept { get; set; }

        /// <summary>
        /// Custom error message to use of party creation is denied.
        /// </summary>
        public string? ErrorMessage { get; set; }

        internal PartyCreationContext(PartyRequestDto partyRequest)
        {
            PartyRequest = partyRequest;
            Accept = true;
        }
    }

    /// <summary>
    /// List of circumstances that can trigger a status reset
    /// </summary>
    public enum PartyMemberReadyStateResetEventType
    {
        /// <summary>
        /// The party settings were updated by the party leader.
        /// </summary>
        PartySettingsUpdated = 1,

        /// <summary>
        /// The data associated with a party member were updated.
        /// </summary>
        PartyMemberDataUpdated = 2,

        /// <summary>
        /// The member list in the party was updated.
        /// </summary>
        PartyMembersListUpdated = 4,

    }

    /// <summary>
    /// Context for <see cref="IPartyEventHandler.OnPlayerReadyStateReset(PartyMemberReadyStateResetContext)"/>
    /// </summary>
    public class PartyMemberReadyStateResetContext
    {

        internal PartyMemberReadyStateResetContext(PartyMemberReadyStateResetEventType eventType, ISceneHost scene, IPartyService party)
        {
            PartyScene = scene;
            Party = party;
            EventType = eventType;
            ShouldReset = true;
        }

        /// <summary>
        /// Circumstance that triggered the event.
        /// </summary>
        public PartyMemberReadyStateResetEventType EventType { get;  }

        /// <summary>
        /// Gets or sets a boolean value indicating whether the ready status should be reset.
        /// </summary>
        public bool ShouldReset { get; set; } = true;

        /// <summary>
        /// Party scene that triggered the event.
        /// </summary>
        public ISceneHost PartyScene { get;  }

        /// <summary>
        /// Party the event
        /// </summary>
        public IPartyService Party { get; }

    }

    /// <summary>
    /// Context used as argument for <see cref="IPartyEventHandler.OnUpdateSettings(PartySettingsUpdateCtx)"/>.
    /// </summary>
    public class PartySettingsUpdateCtx
    {
        /// <summary>
        /// Gets the party which triggered the event.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the new settings.
        /// </summary>
        public PartySettingsDto Config { get; }

        /// <summary>
        /// Gets or sets a boolean indicating if the changes should be applied.
        /// </summary>
        public bool ApplyChanges { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional error message.
        /// </summary>
        public string ErrorMsg { get; set; } = "";

        internal PartySettingsUpdateCtx(IPartyService party, PartySettingsDto config)
        {
            Party = party;
            Config = config;
        }
    }

    /// <summary>
    /// Context provided when firing the JoiningParty event.
    /// </summary>
    public class JoiningPartyContext
    {
        /// <summary>
        /// Party.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Session.
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// Gets the user peer object.
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// The number of users who:
        /// - Are members of the party, or
        /// - Are currently in the connection process and have been accepted by <code>IPartyEventHandler.OnJoining</code> event handlers, but are not fully connected yet.
        /// </summary>
        /// <remarks>
        /// When you want to limit the number of users who can join the party in a <code>IPartyEventHandler.OnJoining</code> event handler,
        /// you should check this number instead of <code>Party.PartyMembers.Count</code>.
        /// </remarks>
        public int TotalOccupiedSlots { get; }

        /// <summary>
        /// Put false to deny the join (if the party can not be joined).
        /// </summary>
        public bool Accept { get; set; } = true;

        /// <summary>
        /// Reason for party join denied.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets the user data provided by the client.
        /// </summary>
        public byte[] UserData { get; }

        internal JoiningPartyContext(IPartyService party, Session session, IScenePeerClient peer, int slots, byte[] userData)
        {
            Party = party;
            Session = session;
            Peer = peer;
            TotalOccupiedSlots = slots;
            UserData = userData;
        }
    }

    /// <summary>
    /// Context object for the <see cref="IPartyEventHandler.OnJoinDenied(JoinDeniedContext)"/> event.
    /// </summary>
    public class JoinDeniedContext
    {
        /// <summary>
        /// Gets the party which triggered the event.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the session of the user whose join was denied.
        /// </summary>
        public Session Session { get; }

        internal JoinDeniedContext(IPartyService party, Session session)
        {
            Party = party;
            Session = session;
        }
    }

    /// <summary>
    /// Context object for the <see cref="IPartyEventHandler.OnJoined(JoinedPartyContext)"/> event.
    /// </summary>
    public class JoinedPartyContext
    {
        /// <summary>
        /// Gets the party the event originates from.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the Session object of the user who joined the party.
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// Gets the network peer associated with the player who joined the party.
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// Gets or sets user data stored with the member.
        /// </summary>
        public byte[] UserData { get; set; }
        internal JoinedPartyContext(IPartyService party, IScenePeerClient peer, Session session, byte[] userData)
        {
            Party = party;
            Peer = peer;
            Session = session;
            UserData = userData;
        }
    }


    /// <summary>
    /// Context object for the <see cref="IPartyEventHandler.OnJoined(JoinedPartyContext)"/> event.
    /// </summary>
    public class PreJoinedPartyContext
    {
        /// <summary>
        /// Gets the party the event originates from.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the Session object of the user who joined the party.
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// Gets the network peer associated with the player who joined the party.
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// Gets or sets user data stored with the member.
        /// </summary>
        public byte[] UserData { get; set; }
        internal PreJoinedPartyContext(IPartyService party, IScenePeerClient peer, Session session, byte[] userData)
        {
            Party = party;
            Peer = peer;
            Session = session;
            UserData = userData;
        }

        /// <summary>
        /// Gets or sets an error that will cancel connection to the party if set to a non null value.
        /// </summary>
        public string? ErrorId { get; set; }
    }


    /// <summary>
    /// Context passed to <see cref="IPartyEventHandler.OnQuit(QuitPartyContext)"/>
    /// </summary>
    public class QuitPartyContext
    {
        /// <summary>
        /// Gets or sets the party that triggered the event.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets or sets the disconnection info.
        /// </summary>
        public DisconnectedArgs Args { get; }

        internal QuitPartyContext(IPartyService party, DisconnectedArgs args)
        {
            Party = party;
            Args = args;
        }
    }

    /// <summary>
    /// Determines when game finding should start.
    /// </summary>
    public enum GameFinderRequestPolicy
    {
        /// <summary>
        /// Game finding should start when all players are ready.
        /// </summary>
        StartWhenAllMembersReady,

        /// <summary>
        /// Game finding should start immediately.
        /// </summary>
        StartNow,

        /// <summary>
        /// Game finding should not start at the end of the event.
        /// </summary>
        DoNotStart
    }

    /// <summary>
    /// Context passed to <see cref="IPartyEventHandler.OnPlayerReadyStateChanged(PlayerReadyStateContext)"/>.
    /// </summary>
    public class PlayerReadyStateContext
    {
        /// <summary>
        /// Gets the party scene.
        /// </summary>
        public ISceneHost PartyScene { get; }

        /// <summary>
        /// Gets the party.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the member whose ready state changed.
        /// </summary>
        public PartyMember Member { get; }

        /// <summary>
        /// Gets or sets the policy indicating if and when game finding should start.
        /// </summary>
        public GameFinderRequestPolicy GameFinderPolicy { get; set; } = GameFinderRequestPolicy.StartWhenAllMembersReady;

        internal PlayerReadyStateContext(IPartyService party, PartyMember user, ISceneHost scene)
        {
            Party = party;
            Member = user;
            PartyScene = scene;
        }
    }

    /// <summary>
    /// Context object used as the argument of <see cref="IPartyEventHandler.OnUpdatingPlayerReadyState(UpdatingPlayerReadyStateContext)"/>.
    /// </summary>
    public class UpdatingPlayerReadyStateContext
    {
        /// <summary>
        /// Gets the party scene.
        /// </summary>
        public ISceneHost PartyScene { get; }

        /// <summary>
        /// Gets the new ready state of the party member.
        /// </summary>
        public PartyMemberStatus NewStatus { get; }

        /// <summary>
        /// Gets the party.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the member whose ready state changed.
        /// </summary>
        public PartyMember Member { get; }


        /// <summary>
        /// Gets or sets the policy indicating if and when game finding should start.
        /// </summary>
        public GameFinderRequestPolicy GameFinderPolicy { get; set; } = GameFinderRequestPolicy.StartWhenAllMembersReady;


        /// <summary>
        /// Set to false to refuse the ready state update.
        /// </summary>
        public bool Accept { get; set; } = true;

        /// <summary>
        /// Sets the error id to return to the player if the handler refused the state change.
        /// </summary>
        public string ErrorId { get; set; } = string.Empty;

        internal UpdatingPlayerReadyStateContext(IPartyService party, PartyMember user, ISceneHost scene, PartyMemberStatus newStatus)
        {
            Party = party;
            Member = user;
            PartyScene = scene;
            NewStatus = newStatus;
        }
    }

    /// <summary>
    /// This context allows altering Party Settings update messages for each member.
    /// </summary>
    public class PartySettingsMemberUpdateCtx
    {
        /// <summary>
        /// Party Service reference. You should not await party operations in this context.
        /// </summary>
        public IPartyService Party { get; }
        /// <summary>
        /// Party settings update message to be sent to each member
        /// </summary>
        public Dictionary<PartyMember, PartySettingsUpdateDto> UpdatesPerMember { get; }

        internal PartySettingsMemberUpdateCtx(IPartyService party, Dictionary<PartyMember, PartySettingsUpdateDto> updatesPerMember)
        {
            Party = party;
            UpdatesPerMember = updatesPerMember;
        }
    }

    /// <summary>
    /// Context object used by <see cref="IPartyEventHandler.OnUpdatingPartyMemberData(UpdatingPartyMemberDataContext)"/>
    /// </summary>
    public class UpdatingPartyMemberDataContext
    {
        internal UpdatingPartyMemberDataContext(PartyMember member,IEnumerable<Models.LocalPlayerInfos> localPlayers, byte[] newData, ISceneHost scene, IPartyService party)
        {
            PartyScene = scene;
            Party = party;
            PartyMember = member;
            LocalPlayers = localPlayers;
            NewUserData = newData;
        }
        /// <summary>
        /// The current party member to update.
        /// </summary>
        public PartyMember PartyMember { get; }

        /// <summary>
        /// Number of local players on the party member.
        /// </summary>
        public IEnumerable<Models.LocalPlayerInfos> LocalPlayers { get; }

        /// <summary>
        /// Gets or sets the new content that should replace the current party member data.
        /// </summary>
        public byte[] NewUserData { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the update should happen or be denied.
        /// </summary>
        public bool IsUpdateValid { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional error message to use if the update is denied.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets the scene of the party.
        /// </summary>
        public ISceneHost PartyScene { get; }

        /// <summary>
        /// Gets the party service.
        /// </summary>
        public IPartyService Party { get; }
    }

    /// <summary>
    /// Extensibility contract for parties
    /// </summary>
    public interface IPartyEventHandler
    {
        /// <summary>
        /// Fired when a client makes a party creation request.
        /// </summary>
        /// <remarks>
        /// This event gives you a chance to validate the party parameters passed by the client.
        /// To refuse party creation, set <c>ctx.Accept</c> to <c>false</c> (it is <c>true</c> by default).
        /// If you refuse party creation, you can set <c>ctx.ErrorMessage</c> to be sent back to the client.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnCreatingParty(PartyCreationContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a request to update the party settings is issued by the party leader.
        /// </summary>
        /// <remarks>
        /// You must NOT call Party APIs that modify the party's state from this handler, or else a deadlock would occur.
        /// This event is fired before the settings are applied.
        /// It gives the application a chance to review the new settings, and possibly reject them by setting <c>ctx.Accept</c> to <c>false</c>.
        /// Optionally, <c>ctx.ErrorMsg</c> can be set to a string that will be sent back to the client as a failure message when <c>ctx.Accept</c> is <c>false.</c>
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnUpdatingSettings(PartySettingsUpdateCtx ctx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a settings update has been applied.
        /// </summary>
        /// <remarks>
        /// You must NOT call Party APIs that modify the party's state from this handler, or else a deadlock would occur.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnUpdateSettings(PartySettingsUpdateCtx ctx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a settings update message is about to be sent to each party member.
        /// </summary>
        /// <param name="ctx">Context of the settings update message.</param>
        /// <returns></returns>
        Task OnSendingSettingsUpdateToMembers(PartySettingsMemberUpdateCtx ctx) { return Task.CompletedTask; }

        /// <summary>
        /// Fired when a new user is connecting to the party.
        /// </summary>
        /// <remarks>
        /// You must NOT call Party APIs that modify the party's state from this handler, or else a deadlock would occur.
        /// This event gives the application a chance to deny entry to the party, by setting <c>ctx.Accept</c> to <c>false</c> (it is <c>true</c> by default).
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnJoining(JoiningPartyContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a connecting user has been denied entry, either by an OnJoining handler or an external component.
        /// </summary>
        /// <remarks>
        /// It is safe to call Party APIs that modify the party's state from this handler.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnJoinDenied(JoinDeniedContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a new user has entered the party, if they were accepted by <c>OnJoining</c> handlers.
        /// </summary>
        /// <remarks>
        /// It is safe to call Party APIs that modify the party's state from this handler.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnJoined(JoinedPartyContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a member quits the party (for any reason).
        /// </summary>
        /// <remarks>
        /// It is safe to call Party APIs that modify the party's state from this handler.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnQuit(QuitPartyContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event fired when the custom data associated with a member are updating
        /// </summary>
        /// <param name="ctx"></param>
        /// <remarks>
        /// This event enables validating the data an denying the change.
        /// </remarks>
        /// <returns></returns>
        Task OnUpdatingPartyMemberData(UpdatingPartyMemberDataContext ctx) => Task.CompletedTask;


        /// <summary>
        /// Fired before setting a player as ready (or NotReady) from the client. This can be used to validate player custom data before setting them as ready.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnUpdatingPlayerReadyState(UpdatingPlayerReadyStateContext ctx) => Task.CompletedTask;
       
        /// <summary>
        /// Fired when a member updates their party status (ready/not ready).
        /// </summary>
        /// <remarks>
        /// You must NOT call Party APIs that modify the party's state from this handler, or else a deadlock would occur.
        /// The updated status can be retrieved in <c>ctx.Member.StatusInParty</c>.
        /// The application can choose whether to start a GameFinder request for the party by setting <c>ctx.GameFinderPolicy</c>.
        /// The default behavior is to launch the request when all members have set their status to Ready.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnPlayerReadyStateChanged(PlayerReadyStateContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event fired when the ready status of a player will be automatically reset to NotReady. 
        /// </summary>
        /// <param name="ctx"></param>
        /// <remarks>An handler code can prevent the reset from occurring by setting <see cref="PartyMemberReadyStateResetContext.ShouldReset"/> to false.</remarks>
        /// <returns></returns>
        Task OnPlayerReadyStateReset(PartyMemberReadyStateResetContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event fired just before the effective joining process is executed.
        /// </summary>
        /// <param name="joinedCtx"></param>
        /// <returns></returns>
        Task OnPreJoined(PreJoinedPartyContext joinedCtx) => Task.CompletedTask;

        /// <summary>
        /// Fired whenever the state of the game finder changes.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task OnGameFinderStateChanged(GameFinderStateChangedContext context) => Task.CompletedTask;

        /// <summary>
        /// Fired when a reservation is created. Enabled handlers to refuse the reservation.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnCreatingReservation(CreateReservationContext ctx) => Task.CompletedTask;
    }

    /// <summary>
    /// Context used as argument for <see cref="IPartyEventHandler.OnCreatingReservation(CreateReservationContext)"/>
    /// </summary>
    public class CreateReservationContext
    {
        internal CreateReservationContext(IPartyService party, ISceneHost scene, PartyReservation reservation)
        {
            Party = party;
            Scene = scene;
            Reservation = reservation;
        }

        /// <summary>
        /// Gets the party.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the party scene.
        /// </summary>
        public ISceneHost Scene { get; }

        /// <summary>
        /// Gets a <see cref="PartyReservation"/> object representing the reservation being evaluated.
        /// </summary>
        public PartyReservation Reservation { get; }

        /// <summary>
        /// Accepts the reservation.
        /// </summary>
        public bool Accept { get; set; } = true;

    }
    /// <summary>
    /// State of the interaction between the party and the game finder.
    /// </summary>
    public enum PartyGameFinderStateChange
    {
        /// <summary>
        /// The game finder is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// A game finder start request was sent.
        /// </summary>
        /// <remarks>
        /// The game finder request might be cancelled.
        /// </remarks>
        StartPending,

        /// <summary>
        /// The game finder is running.
        /// </summary>
        Started,
    }
    /// <summary>
    /// Context used as argument for <see cref="IPartyEventHandler.OnGameFinderStateChanged(GameFinderStateChangedContext)"/>.
    /// </summary>
    public class GameFinderStateChangedContext
    {
        internal GameFinderStateChangedContext(IPartyService party, ISceneHost scene,PartyGameFinderStateChange oldState, PartyGameFinderStateChange newState)
        {
            Party = party;
            Scene = scene;
            OldState = oldState;
            NewState = newState;
        }

        /// <summary>
        /// Gets the party.
        /// </summary>
        public IPartyService Party { get; }

        /// <summary>
        /// Gets the party scene.
        /// </summary>
        public ISceneHost Scene { get; }

        /// <summary>
        /// Gets the previous state.
        /// </summary>
        public PartyGameFinderStateChange OldState { get; }

        /// <summary>
        /// Gets the new state.
        /// </summary>
        public PartyGameFinderStateChange NewState { get; }
    }
}
