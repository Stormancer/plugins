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
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// Provides a way of pushing analytics in the system.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Pushes a document in the analytics system.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="category"></param>
        /// <param name="document"></param>
        void Push(string type,string category, JObject document);

        /// <summary>
        /// Pushes a document in the analytics system.
        /// </summary>
        /// <param name="document"></param>
        void Push(AnalyticsDocument document);


        /// <summary>
        /// Flushes the documents currently waiting for save.
        /// </summary>
        /// <returns></returns>
        Task Flush();
    }

    /// <summary>
    /// Represents an analytics storage output.
    /// </summary>
    public interface IAnalyticsOutput
    {
       /// <summary>
       /// Saves the documents in the storage output.
       /// </summary>
       /// <param name="store"></param>
       /// <param name="docs"></param>
       /// <returns></returns>
        Task Flush(string store, IEnumerable<AnalyticsDocument> docs);
    }
}
