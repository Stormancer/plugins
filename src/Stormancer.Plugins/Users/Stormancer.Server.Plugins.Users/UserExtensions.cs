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

using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Users;
using System;
using System.Diagnostics.CodeAnalysis;
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
            if (session.SessionData.TryGetValue(key, out var data) && serializer.TryDeserialize<T>(new System.Buffers.ReadOnlySequence<byte>(data), out T result, out _))
            {
                return result;
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

        /// <summary>
        /// Tries getting an option stored on the user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="user"></param>
        /// <param name="key"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static bool TryGetOption<T>(this User user, string key, [NotNullWhen(true)] out T? option)
        {
            if (user.UserData.TryGetValue("options", out var value) && value is JObject r)
            {
                if (r.TryGetValue(key, out var optionToken) && optionToken is JObject optionObj)
                {
                    option = optionObj.ToObject<T>()!;
                    return true;
                }
            }

            option = default;
            return false;


        }

        /// <summary>
        /// Sets an option on the provided user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="user"></param>
        /// <param name="key"></param>
        /// <param name="option"></param>
        public static void SetOption<T>(this User user, string key, T option) where T:class
        {
            ArgumentNullException.ThrowIfNull(option, nameof(option));
            
            if(!(user.UserData.TryGetValue("options",out var token ) && token is JObject options))
            {
                options  = new JObject();
                user.UserData["options"] = options;
            }

            options[key] = JObject.FromObject(option);
            
        }
    }
}

