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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Provides APIs to create or join parties.
    /// </summary>
    public interface IPartyManagementService
    {
        /// <summary>
        /// Creates a party.
        /// </summary>
        /// <param name="partyRequest"></param>
        /// <param name="leaderUserId"></param>
        /// <returns></returns>
        Task<string> CreateParty(PartyRequestDto partyRequest, string leaderUserId);

        /// <summary>
        /// Creates a connection token to a party using an invitation code.
        /// </summary>
        /// <param name="invitationCode"></param>
        /// <param name="userData"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string?> CreateConnectionTokenFromInvitationCodeAsync(string invitationCode,Memory<byte> userData, CancellationToken cancellationToken);
        
        /// <summary>
        /// Creates a connection token to a party using a party id.
        /// </summary>
        /// <param name="partyId"></param>
        /// <param name="userData"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Result<string, string>> CreateConnectionTokenFromPartyId(string partyId, Memory<byte> userData, CancellationToken cancellationToken);
    }
}
