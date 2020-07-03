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
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    public class ProfileCtx
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JObject>> _data;


        public ProfileCtx(
            IEnumerable<string> userIds,
            ConcurrentDictionary<string, ConcurrentDictionary<string, JObject>> data,
            Dictionary<string, string> displayOptions,
            Session origin)
        {
            Users = userIds;
            _data = data;
            DisplayOptions = displayOptions;
            Origin = origin;
        }

        /// <summary>
        /// The list of user ids we are computing profile information for.
        /// </summary>
        public IEnumerable<string> Users { get; }

        /// <summary>
        /// Display options for profile generation.
        /// </summary>
        public Dictionary<string, string> DisplayOptions { get; }

        /// <summary>
        /// Origin of the request.
        /// </summary>
        public Session Origin { get; }

        /// <summary>
        /// Update profile data in the result.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="key"></param>
        /// <param name="updater"></param>
        public void UpdateProfileData(string userId, string key, Func<JObject,JObject> updater)
        {
            var data = _data.GetOrAdd(userId, (string id) => new ConcurrentDictionary<string, JObject>());

            data.AddOrUpdate(key, i =>
            {
                var json = new JObject();
                return updater(json);
                
            }, (i, old) =>
             {
                 return updater(old);
             });
        }

    }

    /// <summary>
    /// Classes implementing the <see cref="IProfilePartBuilder"/> interface  participate in the profile generation process.
    /// </summary>
    public interface IProfilePartBuilder
    {

        Task GetProfiles(ProfileCtx ctx);
    }
}
