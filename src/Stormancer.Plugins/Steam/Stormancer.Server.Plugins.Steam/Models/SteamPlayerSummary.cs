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

using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Steam
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Steam player summary.
    /// </summary>
    public class SteamPlayerSummary
    {
        /// <summary>
        /// Steam avatar.
        /// </summary>
        public string? avatar { get; set; }

        /// <summary>
        /// Steam full avatar.
        /// </summary>
        public string? avatarfull { get; set; }

        /// <summary>
        /// Steam medium avatar.
        /// </summary>
        public string? avatarmedium { get; set; }

        /// <summary>
        /// Steam community profile visibility state.
        /// </summary>
        public int communityvisibilitystate { get; set; }

        /// <summary>
        /// Last log off data.
        /// </summary>
        public int lastlogoff { get; set; }

        /// <summary>
        /// Location city id.
        /// </summary>
        public int loccityid { get; set; }

        /// <summary>
        /// Location country code.
        /// </summary>
        public string? loccountrycode { get; set; }

        /// <summary>
        /// Location state code.
        /// </summary>
        public string? locstatecode { get; set; }

        /// <summary>
        /// Pseudonym.
        /// </summary>
        public string? personaname { get; set; }

        /// <summary>
        /// Steam connection state
        /// </summary>
        public int personastate { get; set; }

        /// <summary>
        /// Steam primary clan Id.
        /// </summary>
        public ulong primaryclanid { get; set; }

        /// <summary>
        /// Steam profile state.
        /// </summary>
        public int profilestate { get; set; }

        /// <summary>
        /// Steam community profile url.
        /// </summary>
        public string? profileurl { get; set; }

        /// <summary>
        /// Steam real name.
        /// </summary>
        public string? realname { get; set; }

        /// <summary>
        /// Steam Id.
        /// </summary>
        public ulong steamid { get; set; }

        /// <summary>
        /// Steam account creation date.
        /// </summary>
        public int timecreate { get; set; }
    }

    internal class SteamPlayerSummariesResponse
    {
        public SteamPlayerSummaries? response { get; set; }
    }

    internal class SteamPlayerSummaries
    {
        public List<SteamPlayerSummary>? players { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
