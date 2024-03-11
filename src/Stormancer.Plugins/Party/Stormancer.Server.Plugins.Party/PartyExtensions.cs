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
using Stormancer.Server.Plugins.Party;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer
{
    /// <summary>
    /// Provides extension methods to add party related features to a scene.
    /// </summary>
    public static class PartyExtensions
    {
        /// <summary>
        /// Adds party features to a scene.
        /// </summary>
        /// <param name="scene"></param>
        public static void AddParty(this ISceneHost scene)
        {
            scene.TemplateMetadata[PartyConstants.METADATA_KEY] = PartyController.PROTOCOL_VERSION;
            scene.TemplateMetadata[PartyService.REVISION_METADATA_KEY] = PartyService.REVISION;
        }

      
        /// <summary>
        /// Tries getting the current gamesession id.
        /// </summary>
        /// <param name="partyService"></param>
        /// <param name="gamesessionId"></param>
        /// <returns></returns>
        public static bool TryGetCurrentGameSessionId(this IPartyService partyService, [NotNullWhen(true)] out string? gamesessionId)
        {
            // partySettings.PublicServerData["stormancer.partyStatus"] = "gamesession";
            //partySettings.PublicServerData["stormancer.partyStatus.details"] = gamesessionId;
            if(partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus",out var status) 
                && status == "gamesession"
                && partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus.details",out gamesessionId))
            {
                return true;
            }
            else
            {
                gamesessionId = null;
                return false;
            }
        }
    }
}
