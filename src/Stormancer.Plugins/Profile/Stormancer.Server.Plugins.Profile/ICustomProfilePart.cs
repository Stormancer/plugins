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

using System.IO;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    /// <summary>
    /// Represents a custom profile part that the client can modify.
    /// </summary>
    public interface ICustomProfilePart
    {
        /// <summary>
        /// Updates a part.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task UpdateAsync(UpdateCustomProfilePartContext ctx);

        /// <summary>
        /// Gets data about a part.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task GetAsync(ProfileCtx ctx);

        /// <summary>
        /// Deletes a part.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="partId"></param>
        /// <param name="fromClient"></param>
        /// <returns></returns>
        Task DeleteAsync(string userId, string partId, bool fromClient);

    }

    public class UpdateCustomProfilePartContext
    {
        internal UpdateCustomProfilePartContext(string userId, string partId, string formatVersion, bool fromClient, Stream data)
        {
            UserId = userId;
            PartId = partId;
            FormatVersion = formatVersion;
            FromClient = fromClient;
            Data = data;
        }
        public string UserId { get; }
        public string PartId { get; }

        public string FormatVersion { get; }
        public bool FromClient { get; }

        public Stream Data { get; }

        public bool Processed { get; set; }
    }
}