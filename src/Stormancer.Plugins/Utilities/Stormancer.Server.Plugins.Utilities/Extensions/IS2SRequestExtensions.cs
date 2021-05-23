using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer
{

    /// <summary>
    /// Extension methods for <see cref="PipeReader"/> and <see cref="PipeWriter"/>
    /// </summary>
    public static class PipeExtensions
    {
        /// <summary>
        /// Reads data from a <see cref="System.IO.Pipelines.PipeReader"/> instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="serializer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async static IAsyncEnumerable<T> ReadObjectsSequence<T>(this PipeReader reader, ISerializer serializer,[EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ISerializer.DeserializationResult<T> result;
            while(true)
            {
                result = await serializer.TryDeserializeAsync<T>(reader, cancellationToken);
                if (result.Success)
                {
                    yield return result.Value;
                }
                else
                {
                    await reader.CompleteAsync();
                    yield break;
                }

            }
        }

        /// <summary>
        /// Reads an object from a <see cref="System.IO.Pipelines.PipeReader"/> instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="serializer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<T> ReadObject<T>(this PipeReader reader, ISerializer serializer, CancellationToken cancellationToken)
        {
            return serializer.DeserializeAsync<T>(reader, cancellationToken);
        }

        /// <summary>
        /// Writes an object to a <see cref="PipeWriter"/> instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="writer"></param>
        /// <param name="data"></param>
        /// <param name="serializer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task WriteObject<T>(this PipeWriter writer, T data, ISerializer serializer, CancellationToken cancellationToken)
        {
            return serializer.SerializeAsync(data, writer, cancellationToken);
        }
    }
}
