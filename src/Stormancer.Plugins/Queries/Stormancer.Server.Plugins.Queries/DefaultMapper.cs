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
            return JsonMapper(null, document);
        }

        /// <summary>
        /// Mapping function
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="document"></param>
        /// <returns></returns>
        public static IEnumerable<Lucene.Net.Index.IIndexableField> JsonMapper(string? prefix, JObject document)
        {
            foreach (var (fieldName, field) in document)
            {
                if (field is not null)
                {

                    var luceneFieldId = prefix != null ? $"{prefix}.{fieldName}" : fieldName;
                    switch (field.Type)
                    {
                        case JTokenType.String:

                            yield return new StringField(luceneFieldId, field.ToObject<string>(), Field.Store.NO);
                            break;
                        case JTokenType.Boolean:

                            yield return new Int32Field(luceneFieldId, field.ToObject<bool>() ? 1 : 0, Field.Store.NO);
                            break;
                        case JTokenType.Integer:
                            yield return new Int64Field(luceneFieldId, field.ToObject<long>(), Field.Store.NO);
                            break;
                        case JTokenType.Float:
                            yield return new DoubleField(luceneFieldId, field.ToObject<double>(), Field.Store.NO);
                            break;
                        case JTokenType.Object:
                            foreach (var indexedField in JsonMapper(luceneFieldId, (JObject)field))
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
