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

using MessagePack;
using Stormancer.Server.Plugins.Party.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Stormancer.Server.Plugins.Party.Dto
{
    /// <summary>
    /// Sent by the leader client to the server through UpdatePartySettings
    /// </summary>
    [MessagePackObject]
    public class PartySettingsDto : IEquatable<PartySettingsDto>
    {
        /// <summary>
        /// Gets or sets the name of the selected GameFinder.
        /// </summary>
        [Key(0)]
        public string GameFinderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets customData
        /// </summary>
        [Key(1)]
        public string CustomData { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating if only the leader can invite players in the group.
        /// </summary>
        [Key(2)]
        public bool OnlyLeaderCanInvite { get; set; } = true;

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating if players can join the party.
        /// </summary>
        [Key(3)]
        public bool IsJoinable { get; set; } = true;

        /// <summary>
        /// Gets or sets public server data.
        /// </summary>
        /// <remarks>
        /// This property is ignored for msgpack serialization because the client can't update it.
        /// </remarks>
        [IgnoreMember]
        public Dictionary<string, string>? PublicServerData { get; set; }

        /// <summary>
        /// Json document used to search the party.
        /// </summary>
        /// <remarks>
        /// Must be a valdi json object.
        /// The party is not searchable if set to empty or an invalid json object.
        /// The content of the document are indexed using the field paths as keys, with '.' as separator.
        /// 
        /// For example, the following document:
        /// {
        ///    "numplayers":3,
        ///    "gamemode":{
        ///      "map":"level3-a",
        ///      "extraFooEnabled":true
        ///    }
        /// }
        /// 
        /// will be indexed with the following keys:
        /// - "numplayers": 3 (numeric)
        /// - "gamemode.map":"level3-a" (string)
        /// - "gamemode.extraFooEnabled":true (bool)
        /// </remarks>
        [Key(4)]
        public string IndexedDocument { get; set; } = string.Empty;

        /// <summary>
        /// Creates a <see cref="PartySettingsDto"/> object.
        /// </summary>
        public PartySettingsDto()
        {
        }

        /// <summary>
        /// Creates a <see cref="PartySettingsDto"/> object.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="partyState"></param>
        public PartySettingsDto(PartyState partyState)
        {
            var config = partyState.Settings;
            GameFinderName = config.GameFinderName;
            CustomData = config.CustomData;
            OnlyLeaderCanInvite = config.OnlyLeaderCanInvite;
            IsJoinable = config.IsJoinable;
            PublicServerData = config.PublicServerData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            IndexedDocument = partyState.SearchDocument?.ToString()??string.Empty;
        }

        /// <summary>
        /// Clones the object.
        /// </summary>
        /// <returns></returns>
        public PartySettingsDto Clone()
        {
            return new PartySettingsDto
            {
                GameFinderName = GameFinderName,
                CustomData = CustomData,
                OnlyLeaderCanInvite = this.OnlyLeaderCanInvite,
                IsJoinable = this.IsJoinable,
                PublicServerData = this.PublicServerData?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }


        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(PartySettingsDto? other) => other switch
        {
            PartySettingsDto dto => dto.GameFinderName == GameFinderName && dto.CustomData == CustomData && dto.OnlyLeaderCanInvite == OnlyLeaderCanInvite && dto.IsJoinable == IsJoinable,
            _ => false
        };
    }

    /// <summary>
    /// Sent by the server to the clients for settings update notification
    /// </summary>
    [MessagePackObject]
    public class PartySettingsUpdateDto
    {
        internal const string Route = "party.settingsUpdated";

        /// <summary>
        /// Gets or sets the name of the gamefinder currently selected.
        /// </summary>
        [Key(0)]
        public string GameFinderName { get; set; }

        /// <summary>
        /// Gets or sets custom data associated with the party.
        /// </summary>
        [Key(1)]
        public string CustomData { get; set; }

        /// <summary>
        /// Gets the settings version.
        /// </summary>
        /// <remarks>
        /// The settings version is incremented each time they are changed. The client can use that to determine if it is up to date.
        /// </remarks>
        [Key(2)]
        public int SettingsVersion { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating if invitation is restricted to the leader (true by default)
        /// </summary>
        [Key(3)]
        public bool OnlyLeaderCanInvite { get; set; } = true;

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating if the party can be joined.
        /// </summary>
        [Key(4)]
        public bool IsJoinable { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        [Key(5)]
        public Dictionary<string, string> PublicServerData { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Json document used as a source to index the party in the in memory database for querying.
        /// </summary>
        [Key(6)]
        public string IndexedDocument { get; set; }

        /// <summary>
        /// Gets or sets the party id.
        /// </summary>
        [Key(7)]
        public string PartyId { get; set; } = default!;

        internal PartySettingsUpdateDto(PartyState state)
        {
            GameFinderName = state.Settings.GameFinderName;
            CustomData = state.Settings.CustomData;
            SettingsVersion = state.SettingsVersionNumber;
            OnlyLeaderCanInvite = state.Settings.OnlyLeaderCanInvite;
            IsJoinable = state.Settings.IsJoinable;
            PublicServerData = state.Settings.PublicServerData;
            IndexedDocument = state.SearchDocument?.ToString()??string.Empty;
            PartyId = state.Settings.PartyId;
        }

        public PartySettingsUpdateDto()
        {
            // Required for serialization to work
        }
    }
}
