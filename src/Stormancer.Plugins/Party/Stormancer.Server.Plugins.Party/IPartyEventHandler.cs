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
    public class PartyCreationContext
    {
        public PartyRequestDto PartyRequest { get; }
        public bool Accept { get; set; }
        public string ErrorMessage { get; set; }

        public PartyCreationContext(PartyRequestDto partyRequest)
        {
            PartyRequest = partyRequest;
            Accept = true;
        }
    }

    public class PartySettingsUpdateCtx
    {
        public IPartyService Party { get; }
        public PartySettingsDto Config { get; }
        public bool ApplyChanges { get; set; } = true;
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

        internal JoiningPartyContext(IPartyService party, Session session, int slots)
        {
            Party = party;
            Session = session;
            TotalOccupiedSlots = slots;
        }
    }

    public class JoinDeniedContext
    {
        public IPartyService Party { get; }

        public Session Session { get; }

        internal JoinDeniedContext(IPartyService party, Session session)
        {
            Party = party;
            Session = session;
        }
    }

    public class JoinedPartyContext
    {
        public IPartyService Party { get; }
        public Session Session { get; }

        internal JoinedPartyContext(IPartyService party, Session session)
        {
            Party = party;
            Session = session;
        }
    }

    public class QuitPartyContext
    {
        public IPartyService Party { get; }
        public DisconnectedArgs Args { get; }

        internal QuitPartyContext(IPartyService party, DisconnectedArgs args)
        {
            Party = party;
            Args = args;
        }
    }

    public enum GameFinderRequestPolicy
    {
        StartWhenAllMembersReady,
        StartNow,
        DoNotStart
    }

    public class PlayerReadyStateContext
    {
        public IPartyService Party { get; }
        public PartyMember Member { get; }
        public GameFinderRequestPolicy GameFinderPolicy { get; set; } = GameFinderRequestPolicy.StartWhenAllMembersReady;

        internal PlayerReadyStateContext(IPartyService party, PartyMember user)
        {
            Party = party;
            Member = user;
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
        Task OnCreatingParty(PartyCreationContext ctx);

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
        Task OnUpdatingSettings(PartySettingsUpdateCtx ctx);

        /// <summary>
        /// Fired when a settings update has been applied.
        /// </summary>
        /// <remarks>
        /// You must NOT call Party APIs that modify the party's state from this handler, or else a deadlock would occur.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnUpdateSettings(PartySettingsUpdateCtx ctx);

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
        Task OnJoining(JoiningPartyContext ctx);

        /// <summary>
        /// Fired when a connecting user has been denied entry, either by an OnJoining handler or an external component.
        /// </summary>
        /// <remarks>
        /// It is safe to call Party APIs that modify the party's state from this handler.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnJoinDenied(JoinDeniedContext ctx);

        /// <summary>
        /// Fired when a new user has entered the party, if they were accepted by <c>OnJoining</c> handlers.
        /// </summary>
        /// <remarks>
        /// It is safe to call Party APIs that modify the party's state from this handler.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnJoined(JoinedPartyContext ctx);

        /// <summary>
        /// Fired when a member quits the party (for any reason).
        /// </summary>
        /// <remarks>
        /// It is safe to call Party APIs that modify the party's state from this handler.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnQuit(QuitPartyContext ctx);

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
        Task OnPlayerReadyStateChanged(PlayerReadyStateContext ctx);
    }
}
