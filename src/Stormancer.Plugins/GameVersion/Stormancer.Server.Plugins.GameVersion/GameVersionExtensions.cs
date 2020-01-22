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
using Stormancer.Server.Plugins.GameVersion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extension methods to Stormancer types for the GameVersion plugin
    /// </summary>
    public static class GameVersionExtensions
    {
        /// <summary>
        /// Add GameVersion functionality to a scene.
        /// </summary>
        /// <remarks>
        /// GameVersion is always enabled on the authenticator scene. Use this method if you want to enable it on additional scenes.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="prefix">Configuration prefix for this instance of GameVersion</param>
        public static void AddGameVersion(this ISceneHost builder, string? prefix = null)
        {
            builder.Metadata[GameVersionPlugin.METADATA_KEY] = prefix ?? "default";
        }
    }
}
