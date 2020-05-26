﻿using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nest.JsonNetSerializer
{
	internal static class JsonReaderExtensions
	{
		public static JToken ReadTokenWithDateParseHandlingNone(this JsonReader reader)
		{
			var dateParseHandling = reader.DateParseHandling;
			reader.DateParseHandling = DateParseHandling.None;
			var token = JToken.ReadFrom(reader);
			reader.DateParseHandling = dateParseHandling;
			return token;
		}

		public static async Task<JToken> ReadTokenWithDateParseHandlingNoneAsync(this JsonReader reader,
			CancellationToken cancellationToken = default(CancellationToken)
		)
		{
			var dateParseHandling = reader.DateParseHandling;
			reader.DateParseHandling = DateParseHandling.None;
			var token = await JToken.ReadFromAsync(reader, cancellationToken).ConfigureAwait(false);
			reader.DateParseHandling = dateParseHandling;
			return token;
		}
	}
}
