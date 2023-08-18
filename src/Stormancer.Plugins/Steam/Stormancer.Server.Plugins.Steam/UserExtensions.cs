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

using J2N;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Steam;
using Stormancer.Server.Plugins.Users;
using System;

namespace Stormancer
{
    /// <summary>
    /// Steam user extensions
    /// </summary>
    public static class SteamUserExtensions
    {
        /// <summary>
        /// Steam user extension to get the steam id of a stormancer user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static ulong? GetSteamId(this User user)
        {
            if (user.Auth.TryGetValue(SteamConstants.PLATFORM_NAME, out var steamAuth))
            {
                var steamId = steamAuth[SteamConstants.ClaimPath];
                if (steamId != null)
                {
                    return ulong.Parse(steamId.ToString());
                }
            }

            return null;
        }

        public static bool TryGetSteamId(this User user, out ulong steamId)
        {
            if (user.Auth.TryGetValue(SteamConstants.PLATFORM_NAME, out var steamAuthSection) && steamAuthSection is JObject steamAuth
                && steamAuth.TryGetValue(SteamConstants.ClaimPath,out var steamIdToken) && steamIdToken is JValue steamIdValue)
            {
                var steamIdString = steamIdValue.ToObject<string>();
              
                if (steamIdString != null)
                {
                    steamId = ulong.Parse(steamIdString.ToString());
                    return true;
                }
            }
            steamId = 0;
            return false;
        }

        /// <summary>
        /// gets the steam App id the player is connected to.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="appId"></param>
        /// <returns></returns>
        public static bool TryGetSteamAppId(this Session session, out uint appId)
        {
            if(session.SessionData.TryGetValue("steam.appId",out var bytes))
            {
                 appId = BitConverter.ToUInt32(bytes,0);
                return true;
            }
            else
            {
                appId = default;
                return false;
            }
        }
    }
}
