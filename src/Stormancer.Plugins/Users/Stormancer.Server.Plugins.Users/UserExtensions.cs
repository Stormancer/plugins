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

namespace Stormancer
{
    public static class UserExtensions
    {


        public static T GetSessionValue<T>(this Session session, string key, ISerializer serializer)
        {
            if (session.SessionData.TryGetValue(key, out var data))
            {
                using (var stream = new MemoryStream(data))
                {
                    return serializer.Deserialize<T>(stream);
                }
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// Gets the pseudo of the user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string GetPseudo(this User user)
        {
            return user?.UserData?["pseudo"]?.ToObject<string>() ?? "unknown";
        }
    }
}

