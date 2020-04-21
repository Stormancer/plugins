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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.DataProtection
{
    /// <summary>
    /// Provides data protection services
    /// </summary>
    public interface IDataProtector
    {
        /// <summary>
        /// Protects data and returns the cyphertext base64url encoded.
        /// </summary>
        /// <param name="value">The binary data to protect.</param>
        /// <param name="policy">The protection policy.</param>
        /// <returns></returns>
        /// <remarks>
        /// The class looks for the configuration key "dataProtection.[usage]" to determine the data protection policy.
        /// </remarks>
        string ProtectBase64Url(byte[] value, string policy);

        /// <summary>
        /// Unprotect data from a base64url encoded cyphertext
        /// </summary>
        /// <param name="value">The cyphertext to unprotect</param>
        /// <param name="defaultPolicy">Default policy to use if no policy is found embedded in the protected string.</param>
        /// <returns>Original unprotected data.</returns>
        byte[] UnprotectBase64Url(string value, string? defaultPolicy = null);
    }

    /// <summary>
    /// Classes implementing this interface provide data protection capabilities.
    /// </summary>
    public interface IDataProtectionProvider
    {
        /// <summary>
        /// Id of the provider.
        /// </summary>
        /// <remarks>
        /// The id must match the provider type associated in the config with the policy passed to
        /// <see cref="IDataProtector.ProtectBase64Url(byte[], string)"/> or 
        /// <see cref="IDataProtector.UnprotectBase64Url(string, string?)"/>
        /// </remarks>
        string Id { get; }

        /// <summary>
        /// Protects data.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="policy"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        byte[] Protect(byte[] value,string policy, JObject config);

        /// <summary>
        /// Unprotects data.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="policy"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        byte[] Unprotect(byte[] value,string policy, JObject config);
    }
}
