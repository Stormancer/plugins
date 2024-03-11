using Microsoft.AspNetCore.Server.IIS.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{

    public static class JObjectExtensions
    {
       
        public static Dictionary<string, object?> ToDictionary(this JObject obj)
        {
            var result = new Dictionary<string, object?>();
            foreach (var (key, value) in obj)
            {
                result[key] = value?.ToObject() ?? null;
            }

            return result;
        }

        public static object? ToObject(this JToken obj)
        {
            return obj switch
            {
                JArray jArray => jArray.ToObject(),
                JObject jObject => jObject.ToDictionary(),
                JValue jValue => jValue.ToObject(),
                _ => throw new NotSupportedException()
            };
        }
        public static object? ToObject(this JValue obj)
        {
            return obj.Value;
        }

        public static object?[] ToObject(this JArray array)
        {
            var result = new object?[array.Count];

            for (int i = 0; i < array.Count; i++)
            {
                result[i] = array[i].ToObject();
            }
            return result;
        }


        /// <summary>
        /// Gets 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="path"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryGetChildByPath(this JObject obj, string path, [NotNullWhen(true)] out JToken? result)
        {
            var els = path.Split('.');
            JObject currentNode = obj;
            JToken? value = currentNode;
            for (int i = 0; i < els.Length; i++)
            {
                var el = els[i];
                if (currentNode == null)
                {
                    result = null;
                    return false;
                }

                value = currentNode[el];
                if (value is JObject)
                {
                    currentNode = (JObject)value;
                }
                else if (i != els.Length - 1)
                {
                    result = null;
                    return false;
                }
            }
            result = value;
            return result != null;
        }


        public static bool TryGetChildByPath<T>(this Dictionary<string, object?> objectTree, string path,  out T? value)
        {
            if (objectTree is null)
            {
                throw new ArgumentNullException(nameof(objectTree));
            }

            var pathSegments = path.Split('.');
            if (pathSegments.Length == 0 || pathSegments.Any(s => string.IsNullOrEmpty(s)))
            {
                throw new ArgumentException($"Invalid path '{path}'");
            }

            IDictionary? currentNode = objectTree;

            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                var segment = pathSegments[i];
               
                if (currentNode.Contains(segment) && currentNode[segment] is IDictionary child)
                {
                    currentNode =child;
                }
                else
                {
                    value = default;
                    return false;
                }


            }
            {
                if (currentNode.Contains(pathSegments.Last()))
                {
                    var v = currentNode[pathSegments.Last()];

                    var token = v!=null? JToken.FromObject(v) : JValue.CreateNull();
                    try
                    {
                        value =token.ToObject<T>();
                        return true;
                    }
                    catch(Exception)
                    {
                        value = default;
                        return false;
                    }
                 
                }
            }

            value = default;
            return false;

        }
        public static void SetChildByPath(this Dictionary<string, object?> objectTree, string path, object value)
        {
            if (objectTree is null)
            {
                throw new ArgumentNullException(nameof(objectTree));
            }

            var pathSegments = path.Split('.');
            if (pathSegments.Length == 0 || pathSegments.Any(s => string.IsNullOrEmpty(s)))
            {
                throw new ArgumentException($"Invalid path '{path}'");
            }

            IDictionary? currentNode = objectTree;

            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                var segment = pathSegments[i];

                static Dictionary<string, object?> CreateChild(IDictionary node, string name)
                {
                    var child = new Dictionary<string, object?>();
                    node.Add(name, child);
                    return child;
                }
                if (currentNode.Contains(segment))
                {
                    var v = currentNode[segment];
                    if (v is IDictionary dict)
                    {
                        currentNode = dict;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot add child to '{v}' (path: {string.Join('.', pathSegments.Take(i))})");
                    }
                }
                else
                {
                    currentNode = CreateChild(currentNode, segment);
                }

            }
            if (value is JToken token)
            {
                currentNode[pathSegments.Last()] = token.ToObject();
            }
            else
            {
                currentNode[pathSegments.Last()] = value;
            }
        }
        public static void SetChildByPath(this JObject obj, string path, JToken value)
        {
            if (obj is null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var pathSegments = path.Split('.');
            if (pathSegments.Length == 0 || pathSegments.Any(s => string.IsNullOrEmpty(s)))
            {
                throw new ArgumentException($"Invalid path '{path}'");
            }

            JObject currentNode = obj;

            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                var segment = pathSegments[i];

                static JObject CreateChild(JObject node, string name)
                {
                    var child = new JObject();
                    node.Add(name, child);
                    return child;
                }
                currentNode = currentNode[segment] switch
                {
                    null => CreateChild(currentNode, segment),
                    JObject v => v,
                    var v => throw new InvalidOperationException($"Cannot add child to '{v}' (path: {string.Join('.', pathSegments.Take(i))})")
                };
            }
            currentNode[pathSegments.Last()] = value;

        }

        // Hash code impl taken from here: https://stackoverflow.com/a/56997271/467638
        public static int ComputeHashCode(this JToken value)
        {
            if (value is null)
            {
                return 0;
            }

            var queue = new Queue<JProperty>();
            foreach (var prop in value.OfType<JProperty>().OrderBy(p => p.Name))
            {
                queue.Enqueue(prop);
            }
            if (queue.Count == 0)
            {
                return 0;
            }

            int hash = 17;
            while (queue.Count > 0)
            {
                JProperty item = queue.Dequeue();
                if (item.Value.HasValues)
                {
                    foreach (var prop in value.OfType<JProperty>().OrderBy(p => p.Name))
                    {
                        queue.Enqueue(prop);
                    }
                }
                else
                {
                    // Hash code combination taken from here: https://stackoverflow.com/a/263416/8088324
                    unchecked
                    {
                        hash = hash * 23 + ComputeHashCode(item.Name);
                        hash = hash * 23 + ComputeHashCode(item.Value.ToString());
                    }
                }
            }
            return hash;
        }

        // Stable hash code for string taken from here: https://stackoverflow.com/a/36845864/8088324
        public static int ComputeHashCode(this string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}
