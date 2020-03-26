using Newtonsoft.Json;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace Stormancer.Server.Plugins.Steam
{
    public static class TokenGenerator
    {
        public static string CreateToken<T>(T data, string key)
        {

            var str = Base64Encode(JsonConvert.SerializeObject(data));
            return string.Format("{0}.{1}", str, ComputeSignature(str, key));
        }

        public static T DecodeToken<T>(string token, string key)
        {

            var el = token.Split('.');
            if (el.Length != 2)
            {
                throw new InvalidOperationException("Invalid token: Parsing error.");
            }
            var data = el[0];
            var sig = el[1];

            if (sig != ComputeSignature(data, key))
            {
                throw new InvalidOperationException("Invalid token: Signature does not match.");
            }
            var claims = JsonConvert.DeserializeObject<T>(Base64Decode(data));

            return claims;
        }

        public static bool TryDecodeTokenData(string token, out string tokenData, params string[] keys)
        {
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            var el = token.Split('.');
            if (el.Length != 2)
            {
                throw new InvalidOperationException("Invalid token: Parsing error.");
            }

            var data = el[0];
            var sig = el[1];
            tokenData = null;
            if (keys.All(key => sig != ComputeSignature(data, key)))
            {
                return false;
            }
            tokenData = Base64Decode(data);

            var expirable = JsonConvert.DeserializeAnonymousType(tokenData, new { Expiration = default(DateTime) });
            if (expirable != null)
            {
                if (expirable.Expiration < DateTime.UtcNow)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryDecodeToken<T>(string token, out T claims, out string selectedKey, out string error, params string[] keys)
        {
            selectedKey = null;
            error = null;
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }
            claims = default(T);
            var el = token.Split('.');
            if (el.Length != 2)
            {
                error = "Tokens should follow the following format: <data>.<sig>";
                return false;
            }
            var data = el[0];
            var sig = el[1];

            selectedKey = keys.FirstOrDefault(key => sig == ComputeSignature(data, key));
            if (selectedKey == null)
            {
                error = $"Invalid key. Signature : {sig}";
                return false;
            }

            return ExtractTokenData(token, out claims, out error);
        }

        public static bool TryDecodeToken<T>(string token, out T claims, out string error, params string[] keys)
        {
            string key;
            return TryDecodeToken(token, out claims, out key, out error, keys);
        }

        /// <summary>
        /// Extracts the data contained in the token, without validating the signature.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="token"></param>
        /// <param name="claims"></param>
        /// <returns></returns>
        public static bool ExtractTokenData<T>(string token, out T claims, out string error)
        {
            error = null;
            var el = token.Split('.');
            var data = el[0];
            var json = Base64Decode(data);
            claims = JsonConvert.DeserializeObject<T>(json);

            var expirable = claims as IExpirable;

            if (expirable != null)
            {
                if (expirable.Expiration < DateTime.UtcNow)
                {
                    error = "Token expired. Current UTC datetime is " + DateTime.UtcNow.ToString() + " Token date: " + expirable.Expiration + " . Token data is " + json;
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// Validate the signature from a token.
        /// </summary>
        /// <remarks>
        /// This method doesn't check the token content, in particular if it is expired.
        /// </remarks>
        /// <param name="token"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public static bool ValidateSignature(string token, params string[] keys)
        {
            var el = token.Split('.');
            if (el.Length != 2)
            {
                return false;
            }
            var data = el[0];
            var sig = el[1];

            if (keys.All(key => sig != ComputeSignature(data, key)))
            {
                return false;
            }
            return true;
        }

        public static byte[] ComputeSignature(ArraySegment<byte> data, byte[] key)
        {
            using (var sha = SHA256CryptoServiceProvider.Create())
            {
                sha.Initialize();
                var buffer = new byte[data.Count + key.Length];
                Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);
                Buffer.BlockCopy(key, 0, buffer, data.Count, key.Length);

                return sha.ComputeHash(buffer);
            }
        }

        private static string ComputeSignature(string data, string key)
        {
            using (var sha = SHA256CryptoServiceProvider.Create())
            {
                sha.Initialize();
                var bytes = System.Text.Encoding.UTF8.GetBytes(data + key);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        public static string Base64Encode(string data)
        {
            try
            {
                var byte_data = System.Text.Encoding.UTF8.GetBytes(data);
                string encodedData = Convert.ToBase64String(byte_data);
                return encodedData;
            }
            catch (Exception e)
            {
                throw new Exception("An error occured during base 64 encoding.", e);
            }
        }

        public static string Base64Decode(string data)
        {
            try
            {
                System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
                System.Text.Decoder utf8Decode = encoder.GetDecoder();
                var byte_data = Convert.FromBase64String(data);
                int charCount = utf8Decode.GetCharCount(byte_data, 0, byte_data.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(byte_data, 0, byte_data.Length, decoded_char, 0);
                string result = new String(decoded_char);
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("An error occured during base 64 decoding.", e);
            }
        }
    }

    public interface IExpirable
    {
        DateTime Expiration { get; }
        DateTime Issued { get; }
    }
}
