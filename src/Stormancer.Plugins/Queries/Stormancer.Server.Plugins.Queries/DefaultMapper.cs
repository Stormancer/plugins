using Lucene.Net.Documents;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Queries
{
    /// <summary>
    /// Provides default mapping functions.
    /// </summary>
    public static class DefaultMapper
    {
        /// <summary>
        /// Generates fields from the json document.
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static IEnumerable<Lucene.Net.Index.IIndexableField> JsonMapper(JObject document)
        {
            return JsonMapper(String.Empty, document);
        }

        public static IEnumerable<Lucene.Net.Index.IIndexableField> JsonMapper(string prefix, JObject document)
        {
            foreach (var (fieldName, field) in document)
            {
                if (field is not null)
                {
                    switch (field.Type)
                    {
                        case JTokenType.String:
                            yield return new StringField($"{prefix}.{fieldName}", field.ToObject<string>(), Field.Store.NO);
                            break;
                        case JTokenType.Boolean:
                            yield return new Int32Field($"{prefix}.{fieldName}", field.ToObject<bool>() ? 1 : 0, Field.Store.NO);
                            break;
                        case JTokenType.Integer:
                            yield return new Int64Field($"{prefix}.{fieldName}", field.ToObject<long>(), Field.Store.NO);
                            break;
                        case JTokenType.Float:
                            yield return new DoubleField($"{prefix}.{fieldName}", field.ToObject<double>(), Field.Store.NO);
                            break;
                        case JTokenType.Object:
                            foreach (var indexedField in JsonMapper($"{prefix}.{fieldName}", (JObject)field))
                            {
                                yield return indexedField;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

    }
}
