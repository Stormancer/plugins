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

using Stormancer.Server.Plugins.Users;
using System.IO;
using System.IO.Pipelines;

namespace Stormancer
{
    /// <summary>
    /// Extension methods for<see cref="User"/> and <see cref="Session"/>.
    /// </summary>
    public static class UserExtensions
    {

        /// <summary>
        /// Gets a value in the session data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="session"></param>
        /// <param name="key"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public static T? GetSessionValue<T>(this Session session, string key, ISerializer serializer)
        {
            if (session.SessionData.TryGetValue(key, out var data))
            {
                var reader = PipeReader.Create(new System.Buffers.ReadOnlySequence<byte>(data));

                return serializer.DeserializeAsync<T>(reader, System.Threading.CancellationToken.None).Result;

            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Gets the pseudo of the user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string? GetPseudo(this User user)
        {
            return user?.Pseudonym;
        }


        /// <summary>
        /// Gets the id of the platform the profile system should use to obtain the pseudonym of the user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string? GetSelectedPlatformForPseudo(this User user)
        {

            if (user.UserData.TryGetValue("selectedPlatform", out var data) && data.Type == Newtonsoft.Json.Linq.JTokenType.String)
            {
                return data.ToObject<string>();
            }
            else
            {
                return user.LastPlatform;
            }
        }
    }
}

