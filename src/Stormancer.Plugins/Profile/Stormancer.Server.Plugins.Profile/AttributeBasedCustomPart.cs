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

using Nest;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    class AttributeBasedCustomPart : ICustomProfilePart
    {
        private readonly IESClientFactory clientFactory;
        private readonly ISerializer serializer;

        public AttributeBasedCustomPart(IESClientFactory client, ISerializer serializer)
        {
            this.clientFactory = client;
            this.serializer = serializer;
        }

        Task<IElasticClient> GetClient(string partId) => clientFactory.CreateClient(partId, "profileParts");
        private string GetProfilePartId(string userId, string partId) => $"{userId}-{partId}";
        private (string, string) ParseProfilePartId(string id)
        {
            var index = id.IndexOf('-');
            return (id.Substring(0, index), id.Substring(index + 1));
        }
        private string GetIndex(string partId) => clientFactory.GetIndex(partId, "profileParts");

        private Dictionary<string, Type>? _customProfilePartTypescache = null;
        private object _syncRoot = new object();

        private bool TryGetCustomProfilePart(string partId, [NotNullWhen(true)] out Type? type)
        {
            lock (_syncRoot)
            {
                if (_customProfilePartTypescache == null)
                {

                    _customProfilePartTypescache = AssemblyLoadContext.Default.Assemblies
                        .Where(a => !a.IsDynamic)
                        .SelectMany(a => a.GetExportedTypes())
                        .Select(t => new { type = t, attr = t.GetCustomAttribute<CustomProfilePartAttribute>() })
                        .Where(t => t.attr != null)
                        .ToDictionary(t => t.attr!.PartId, t => t.type) ?? new Dictionary<string, Type>();

                }

                return _customProfilePartTypescache.TryGetValue(partId, out type);
            }
        }

        public async Task DeleteAsync(string userId, string partId, bool fromClient)
        {
            if (TryGetCustomProfilePart(partId, out var type))
            {
                var client = await GetClient(type.GetCustomAttribute<CustomProfilePartAttribute>()!.PartId);
                await client.DeleteAsync<JObject>(GetProfilePartId(userId, partId));
            }
        }

        public async Task GetAsync(ProfileCtx ctx)
        {
            var partIds = new List<string>();
            foreach (var requestedPart in ctx.DisplayOptions)
            {
                if (TryGetCustomProfilePart(requestedPart.Key, out var type))
                {
                    var partId = type.GetCustomAttribute<CustomProfilePartAttribute>()!.PartId;

                    partIds.Add(partId);
                }
            }

            var client = await clientFactory.CreateClient("default", "profileParts");

            var result = await client.MultiGetAsync(desc =>
            {
                foreach (var partId in partIds)
                {
                    desc = desc.GetMany<JObject>(ctx.Users.Select(u => new Id(GetProfilePartId(u, partId))), (s, id) => s.Index(GetIndex(partId)));
                }
                return desc;
            });

            foreach (var partId in partIds)
            {
                var hits = result.GetMany<JObject>(ctx.Users.Select(u => GetProfilePartId(u, partId)));
                foreach (var hit in hits)
                {
                    if (hit.Found)
                    {
                        var (userId, _) = ParseProfilePartId(hit.Id);
                        ctx.UpdateProfileData(userId, partId, o => hit.Source);
                    }
                }
            }
        }

        public async Task UpdateAsync(string userId, string partId, string formatVersion, bool fromClient, Stream data)
        {
            if (TryGetCustomProfilePart(partId, out var type))
            {
                var client = await GetClient(type.GetCustomAttribute<CustomProfilePartAttribute>()!.PartId);

                var json = serializer.Deserialize<string>(data);
                var jObj = JObject.Parse(json);
                var result = await client.IndexAsync(jObj, rq => rq.Id(GetProfilePartId(userId, partId)));

            }
        }
    }

    /// <summary>
    /// Marks a class as a custom profile part.
    /// </summary>
    public class CustomProfilePartAttribute : Attribute
    {
        /// <summary>
        /// Creates a <see cref="CustomProfilePartAttribute"/> instance.
        /// </summary>
        /// <param name="partId"></param>
        public CustomProfilePartAttribute(string partId)
        {
            PartId = partId;
        }

        /// <summary>
        /// Gets the id of the custom profile part.
        /// </summary>
        public string PartId { get; }
    }
}
