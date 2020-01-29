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

namespace Stormancer.Server.Plugins.Steam.Models
{
    public enum LobbyType
    {
        Private = 0, // The only way to join the lobby is from an invite.
        FriendsOnly = 1, // Joinable by friends and invitees, but does not show up in the lobby list.
        Public = 2, // Returned by search and visible to friends.
        Invisible = 3, // Returned by search, but not visible to other friends. This is useful if you want a user in two lobbies, for example matching groups together. A user can be in only one regular lobby, and up to two invisible lobbies.
        PrivateUnique = 4 // Private unique lobby. Not deleted when empty. Unique by appid/lobby_name. 
    }

    public class SteamCreateLobbyData
    {
        public uint appid { get; set; }
        public string steamid_lobby { get; set; }
    }

    public class SteamCreateLobbyResponse
    {
        public SteamCreateLobbyData response { get; set; }
    }
}
