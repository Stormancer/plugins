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

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Steam lobby types
    /// </summary>
    public enum LobbyType
    {
        /// <summary>
        /// The only way to join the lobby is from an invite.
        /// </summary>
        Private = 0,

        /// <summary>
        /// Joinable by friends and invitees, but does not show up in the lobby list.
        /// </summary>
        FriendsOnly = 1,

        /// <summary>
        /// Returned by search and visible to friends.
        /// </summary>
        Public = 2,

        /// <summary>
        /// Returned by search, but not visible to other friends. This is useful if you want a user in two lobbies, for example matching groups together. A user can be in only one regular lobby, and up to two invisible lobbies.
        /// </summary>
        Invisible = 3,

        /// <summary>
        /// Private unique lobby. Not deleted when empty. Unique by appid/lobby_name.
        /// </summary>
        PrivateUnique = 4
    }

#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Steam create lobby data dto.
    /// </summary>
    public class SteamCreateLobbyData
    {
        /// <summary>
        /// Steam AppId
        /// </summary>
        public uint appid { get; set; }

        /// <summary>
        /// Steam LobbyId
        /// </summary>
        public string? steamid_lobby { get; set; }
    }

    /// <summary>
    /// Steam create lobby response dto
    /// </summary>
    public class SteamCreateLobbyResponse
    {
        /// <summary>
        /// Response data dto.
        /// </summary>
        public SteamCreateLobbyData? response { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
