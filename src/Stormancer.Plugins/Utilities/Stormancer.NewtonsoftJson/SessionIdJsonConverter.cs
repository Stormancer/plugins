using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Json
{
    /// <summary>
    /// Json converter for <see cref="SessionId"/>
    /// </summary>
    public class SessionIdJsonConverter : JsonConverter<SessionId>
    {
        /// <summary>
        /// Reads a <see cref="SessionId"/> from json.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="hasExistingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public override SessionId ReadJson(JsonReader reader, Type objectType, SessionId existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return SessionId.From((string?)reader.Value);
        }

        /// <summary>
        /// Writes a <see cref="SessionId"/> to json.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, SessionId value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
