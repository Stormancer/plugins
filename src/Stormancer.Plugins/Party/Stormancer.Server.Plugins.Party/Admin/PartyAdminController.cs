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

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party.Admin
{
    /// <summary>
    /// Provides information about sessions.
    /// </summary>
    [Route("_parties")]
    public class PartyAdminController : ControllerBase
    {
        private readonly ISceneHost scene;

        /// <summary>
        /// Creates an instance of <see cref="PartyAdminController"/>.
        /// </summary>
        /// <param name="scene"></param>
        public PartyAdminController(ISceneHost scene)
        {
            this.scene = scene;
        }


        /// <summary>
        /// Gets connected questions.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("")]
        public async Task<SearchResult<JObject>> QuerySessions([FromBody] PartySearchRequest request, CancellationToken cancellationToken)
        {
            await using var scope = scene.CreateRequestScope();
            var parties = scope.Resolve<PartySearchService>();
            return await parties.SearchParties(request.Query ?? new JObject(),request.Skip,request.Size, cancellationToken);
           

        }
    }
    /// <summary>
    /// Party search request.
    /// </summary>
    public class PartySearchRequest
    {
        /// <summary>
        /// Number of items to return in the response.
        /// </summary>
        public uint Size { get; set; } = 10;

        /// <summary>
        /// Number of items to skip.
        /// </summary>
        public uint Skip { get; set; } = 0;

        /// <summary>
        /// Query
        /// </summary>
        public JObject? Query { get; set; } 
    }

}
