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

using MsgPack.Serialization;
using Stormancer.Server.Plugins.Party.Model;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.Party.Dto
{
    /// <summary>
    /// Sent by the leader client to the server through UpdatePartySettings
    /// </summary>
    public class PartySettingsDto
    {
        [MessagePackMember(0)]
        public string GameFinderName { get; set; }

        [MessagePackMember(1)]
        public string CustomData { get; set; }

        [MessagePackMember(2)]
        public bool OnlyLeaderCanInvite { get; set; } = true;

        [MessagePackMember(3)]
        public bool IsJoinable { get; set; } = true;

        // This property is ignored because the client can't update it.
        [MessagePackIgnore]
        public Dictionary<string, string> PublicServerData { get; set; } = null;

        public PartySettingsDto()
        {
        }

        public PartySettingsDto(PartyConfiguration config)
        {
            GameFinderName = string.Copy(config.GameFinderName);
            CustomData = string.Copy(config.CustomData);
            OnlyLeaderCanInvite = config.OnlyLeaderCanInvite;
            IsJoinable = config.IsJoinable;
            PublicServerData = config.PublicServerData?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public PartySettingsDto Clone()
        {
            return new PartySettingsDto
            {
                GameFinderName = string.Copy(this.GameFinderName),
                CustomData = string.Copy(this.CustomData),
                OnlyLeaderCanInvite = this.OnlyLeaderCanInvite,
                IsJoinable = this.IsJoinable,
                PublicServerData = this.PublicServerData?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        public override bool Equals(object obj) => obj switch
        {
            PartySettingsDto dto => dto.GameFinderName == GameFinderName && dto.CustomData == CustomData && dto.OnlyLeaderCanInvite == OnlyLeaderCanInvite && dto.IsJoinable == IsJoinable,
            _ => false
        };

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Sent by the server to the clients for settings update notification
    /// </summary>
    public class PartySettingsUpdateDto
    {
        public const string Route = "party.settingsUpdated";

        [MessagePackMember(0)]
        public string GameFinderName { get; set; }

        [MessagePackMember(1)]
        public string CustomData { get; set; }

        [MessagePackMember(2)]
        public int SettingsVersion { get; set; }

        // The member ordering is different from PartySettingsDto to maintain compatibility with older clients
        [MessagePackMember(3)]
        public bool OnlyLeaderCanInvite { get; set; } = true;

        [MessagePackMember(4)]
        public bool IsJoinable { get; set; } = true;

        [MessagePackMember(5)]
        public Dictionary<string, string> PublicServerData { get; set; } = new Dictionary<string, string>();

        internal PartySettingsUpdateDto(PartyState state)
        {
            GameFinderName = state.Settings.GameFinderName;
            CustomData = state.Settings.CustomData;
            SettingsVersion = state.SettingsVersionNumber;
            OnlyLeaderCanInvite = state.Settings.OnlyLeaderCanInvite;
            IsJoinable = state.Settings.IsJoinable;
            PublicServerData = state.Settings.PublicServerData;
        }

        public PartySettingsUpdateDto()
        {
            // Required for serialization to work
        }
    }
}
