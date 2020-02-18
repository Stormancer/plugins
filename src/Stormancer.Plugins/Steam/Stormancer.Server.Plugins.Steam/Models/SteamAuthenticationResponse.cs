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

namespace Stormancer.Server.Plugins.Steam.Models
{
    public class SteamAuthenticationResponse
    {
        public InnerResponse response { get; set; }
    }

    public class InnerResponse
    {
        public ErrorResponse error { get; set; }
        public ParamsResponse @params { get; set; }
    }

    public class ParamsResponse
    {
        public string result { get; set; }
        public ulong steamid { get; set; }
        public ulong ownersteamid { get; set; }
        public bool vacbanned { get; set; }
        public bool publisherbanned { get; set; }
    }

    public class ErrorResponse
    {
        public int errorcode { get; set; }
        public string errordesc { get; set; }
    }
}

