// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server
{
    /// <summary>
    /// A remote operation
    /// </summary>
    public interface IRemotePipe : IAsyncDisposable
    {
        /// <summary>
        /// Gets the writer used to send data.
        /// </summary>
        PipeWriter Writer { get; }
        /// <summary>
        /// Gets the reader used to get returned data.
        /// </summary>
        PipeReader Reader { get; }
    }

    /// <summary>
    /// The result of a scene to scene request.
    /// </summary>
    public class S2SOperation : IRemotePipe
    {
        private readonly Task<IS2SRequest> requestTask;

        /// <summary>
        /// Serializer used for S2S operations.
        /// </summary>
        protected readonly ISerializer serializer;
        private readonly Func<PipeWriter, Task> argsWriter;
        private Pipe inputPipe = new Pipe();
        private Pipe outputPipe = new Pipe();

        /// <summary>
        /// Gets the writer used to write into the query input pipe.
        /// </summary>
        public PipeWriter Writer => inputPipe.Writer;

        /// <summary>
        /// Gets the reader used to read the query output pipe.
        /// </summary>
        public PipeReader Reader => outputPipe.Reader;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="serializer"></param>
        /// <param name="argsWriter"></param>
        /// <param name="cancellationToken"></param>
        public S2SOperation(Task<IS2SRequest> request, ISerializer serializer, Func<PipeWriter,Task> argsWriter, CancellationToken cancellationToken)
        {
            this.requestTask = request;
            this.serializer = serializer;
            this.argsWriter = argsWriter;
            _ = ReadOutput(cancellationToken);
            _ = WriteInput(cancellationToken);
        }

        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            var rq = await requestTask;
           
            await inputPipe.Writer.CompleteAsync();
            await outputPipe.Reader.CompleteAsync();

            await rq.DisposeAsync();

        }

        private async ValueTask ReadOutput(CancellationToken cancellationToken)
        {
            IS2SRequest? rq = null;
            try
            {
                rq = await requestTask;

                await rq.Reader.CopyToAsync(outputPipe.Writer, cancellationToken);

                outputPipe.Writer.Complete();
                rq.Reader.Complete();
            }
            catch (Exception ex)
            {
                rq?.Reader?.Complete(ex);
                outputPipe.Writer.Complete(ex);
            }
        }

        private async ValueTask WriteInput(CancellationToken cancellationToken)
        {
            IS2SRequest? rq = null;
            try
            {
                rq = await requestTask;

                await argsWriter(rq.Writer);
                await inputPipe.Reader.CopyToAsync(rq.Writer, cancellationToken);

                inputPipe.Reader.Complete();
                rq.Writer.Complete();
            }
            catch (Exception ex)
            {
                rq?.Writer?.Complete(ex);
                inputPipe.Reader.Complete(ex);
            }
        }
    }


    /// <summary>
    /// Provides an easy to use interface to get a single result from a S2S operation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class S2SOperationResult<T> : S2SOperation
    {
        private object _syncRoot = new object();
        private Task<T>? _result;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="serializer"></param>
        /// <param name="argsWriter"></param>
        /// <param name="cancellationToken"></param>
        public S2SOperationResult(Task<IS2SRequest> request, ISerializer serializer, Func<PipeWriter, Task> argsWriter, CancellationToken cancellationToken) : base(request, serializer,argsWriter, cancellationToken)
        {
        }

        /// <summary>
        /// Gets the result of the S2S operation.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<T> GetResultAsync(CancellationToken ct = default)
        {
            lock (_syncRoot)
            {
                if (_result == null)
                {
                    async Task<T> ReadObject(CancellationToken ct)
                    {
                        var r = await Reader.ReadObject<T>(serializer, ct);
                        Reader.Complete();
                        return r;
                    }
                    _result = ReadObject(ct);
                    
                }
                return _result;
            }
        }
    }

    /// <summary>
    /// Provides an easy to use interface to get a sequence of results from a S2S operation.
    /// </summary>
    /// <remarks>
    /// The object doesn't support enumerating several times and is not thread safe.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class S2SOperationResults<T> : S2SOperation
    {

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="serializer"></param>
        /// <param name="argsWriter"></param>
        /// <param name="cancellationToken"></param>
        public S2SOperationResults(Task<IS2SRequest> request, ISerializer serializer, Func<PipeWriter, Task> argsWriter, CancellationToken cancellationToken) : base(request, serializer,argsWriter, cancellationToken)
        {
        }

        /// <summary>
        /// Gets an <see cref="IAsyncEnumerable{T}"/> instance enumerating the results of the S2S operation.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public IAsyncEnumerable<T> GetResultsAsync(CancellationToken ct = default)
        {

            return Reader.ReadObjectsSequence<T>(serializer, ct);

        }
    }

}
