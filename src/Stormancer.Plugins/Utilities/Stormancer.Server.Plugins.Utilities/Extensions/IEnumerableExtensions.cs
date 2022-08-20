using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Utilities.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Filter out null values from a collection of nullables.
        /// </summary>
        /// <typeparam name="T">Type of the IEnumerable</typeparam>
        /// <param name="source">IEnumerable to be filtered</param>
        /// <returns>A copy of <paramref name="source"/> without null values.</returns>
        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
            where T : class
        {
            return source.Where(x => x != null)!;
        }

        /// <summary>
        /// Make a single-item IEnumerable from an object.
        /// </summary>
        /// <typeparam name="T">Type of the object</typeparam>
        /// <param name="item">Object to create an IEnumerable from.</param>
        /// <returns>An IEnumerable that contains <paramref name="item"/></returns>
        public static IEnumerable<T> ToEnumerable<T>(this T item)
        {
            yield return item;
        }

        public static async IAsyncEnumerable<T> SelectManyInterlaced<T>(this IEnumerable<IAsyncEnumerable<T>> sources, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<T>();

         
            async Task WriteToChannel(IAsyncEnumerable<T> source, ChannelWriter<T> writer, CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (var item in source.WithCancellation(cancellationToken))
                    {
                        await writer.WriteAsync(item, cancellationToken);
                    }
                }
                catch(Exception ex)
                {

                    channel.Writer.TryComplete(ex);
                }
            }

            async Task CompleteWriterOnAllCompleted(List<Task> tasks, ChannelWriter<T> writer)
            {
                try
                {
                    await Task.WhenAll(tasks);
                    writer.TryComplete();
                }
                catch(Exception ex)
                {
                    writer.TryComplete(ex);
                }
            };
            var list = new List<Task>();
            foreach(var source in sources)
            {
                list.Add(WriteToChannel(source, channel.Writer,cancellationToken));
            }

            _ = CompleteWriterOnAllCompleted(list, channel.Writer);


            while(await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while(channel.Reader.TryRead(out var item ))
                {
                    yield return item;
                }
            }

           
        }
    }
}
