using Stormancer.Core;
using System;
using System.Buffers;
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
        public async static IAsyncEnumerable<T> ReadObjectsSequence<T>(this PipeReader reader, IClusterSerializer serializer, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    var readResult = await reader.ReadAtLeastAsync(1);
                    if (readResult.IsCanceled)
                    {
                        yield break;
                    }

                    reader.AdvanceTo(readResult.Buffer.Start);
                    if (readResult.IsCompleted && readResult.Buffer.IsEmpty)
                    {
                        yield break;
                    }

                    yield return await serializer.DeserializeAsync<T>(reader, cancellationToken);
                }
            }
            finally
            {
                reader.Complete();
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
        public static ValueTask<T> ReadObject<T>(this PipeReader reader, IClusterSerializer serializer, CancellationToken cancellationToken)
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
        /// <returns></returns>
        public static void WriteObject<T>(this IBufferWriter<byte> writer, T data, IClusterSerializer serializer)
        {
            serializer.Serialize(writer, data);
        }
    }
}
