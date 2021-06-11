using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extensions for pipe reader.
    /// </summary>
    public static class PipeReaderExtensions
    {
        public static ValueTask<bool> TryCopyToAsync(this PipeReader reader, PipeWriter writer, CancellationToken cancellationToken = default, Action<int, object?>? onCopied = null, object? userState = null)
        {
            return TryCopyToAsync(reader, writer, false, cancellationToken, onCopied, userState);
        }

        /// <summary>
        /// Copies a reader's content to a writer and notifies data copied.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="writer"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="completeOnEnd"></param>
        /// <param name="onCopied"></param>
        /// <param name="userState"></param>
        /// <returns></returns>
        public static async ValueTask<bool> TryCopyToAsync(this PipeReader reader, PipeWriter writer, bool completeOnEnd = false, CancellationToken cancellationToken = default, Action<int, object?>? onCopied = null, object? userState = null)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            ReadResult result;


            do
            {


                if (!reader.TryRead(out result))
                {
                    result = await reader.ReadAsync(cancellationToken);
                }
                if (result.IsCanceled)
                {
                    return false;
                }
                foreach (var span in result.Buffer)
                {
                    var mem = writer.GetMemory(span.Length);
                    span.CopyTo(mem);

                    await writer.WriteAsync(mem.Slice(0, span.Length));
                    onCopied?.Invoke(span.Length, userState);
                }
                await writer.FlushAsync();
                reader.AdvanceTo(result.Buffer.End);

            }
            while (!result.IsCanceled && !result.IsCompleted);
            if (result.IsCompleted)
            {
                reader.Complete();
            }
            if (completeOnEnd)
            {
                writer.Complete();
            }
            return true;

        }

        /// <summary>
        /// Copies a reader's content to a writer and notifies data copied.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="writer"></param>
        /// <param name="count">number of bytes to copy.</param>
        /// <returns></returns>
        public static async Task<bool> CopyToAsync(this PipeReader reader, PipeWriter writer, int count)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            ReadResult result;
            int consumed = 0;

            do
            {


                if (!reader.TryRead(out result))
                {
                    result = await reader.ReadAsync();
                }
                var toRead = (int)Math.Min(result.Buffer.Length, count - consumed);
                var slice = result.Buffer.Slice(0, toRead);
                var mem = writer.GetMemory(toRead);
                slice.CopyTo(mem.Span);
                writer.Advance(toRead);
                reader.AdvanceTo(result.Buffer.GetPosition(toRead));
                consumed += toRead;
            }
            while (!result.IsCompleted && consumed < count);

            return consumed == count;

        }


        /// <summary>
        /// Copies a reader's content to a writer and notifies data copied.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="stream"></param>
        /// <param name="count">number of bytes to copy.</param>
        /// <returns></returns>
        public static async Task<bool> CopyToAsync(this PipeReader reader, Stream stream, int count)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            ReadResult result;
            int consumed = 0;

            do
            {

                if (!reader.TryRead(out result))
                {
                    result = await reader.ReadAsync();
                }
                var toRead = (int)Math.Min(result.Buffer.Length, count - consumed);
                var slice = result.Buffer.Slice(0, toRead);

                foreach (var item in slice)
                {
                    await stream.WriteAsync(item.Span.ToArray(), 0, item.Span.Length);
                }
                reader.AdvanceTo(slice.End);
                consumed += toRead;
            }
            while (!result.IsCompleted && consumed < count);

            return consumed == count;

        }


        public static async ValueTask CopyToAsync(this PipeReader reader, PipeWriter?[] writers, CancellationToken ct = default, bool closeOnEnd = false)
        {
            ReadResult result;
            do
            {
                result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                for (int i = 0; i < writers.Length; i++)
                {
                    var writer = writers[i];
                    if (writer != null)
                    {
                        var mem = writer.GetMemory((int)buffer.Length);

                        buffer.CopyTo(mem.Span);
                        writer.Advance((int)buffer.Length);
                        await writer.FlushAsync(ct);
                    }
                }
                reader.AdvanceTo(buffer.End, buffer.End);
            }
            while (!result.IsCompleted && !result.IsCanceled);

            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            if (closeOnEnd)
            {
                for (int i = 0; i < writers.Length; i++)
                {

                    writers[i]?.Complete();

                }
            }
        }
    }
}
