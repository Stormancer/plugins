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

using System.Threading;
using System.Threading.Tasks;
using Stormancer.Diagnostics;

namespace Stormancer
{
    /// <summary>
    /// Extension methods for <see cref="Task"/>.
    /// </summary>
    public static class TaskExtensions
    {
       

        /// <summary>
        /// Wait until the task completes, with support for cancellation.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();

            using var registration = cancellationToken.Register(() => { tcs.TrySetCanceled(); });

            await Task.WhenAny(task, tcs.Task);
            if(task.IsCompleted)
            {
                return;
            }
            else
            {
                throw new TaskCanceledException();
            }
        }

        /// <summary>
        /// Wait until the task completes, with support for cancellation.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> WaitAsync<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();

            using var registration = cancellationToken.Register(() => { tcs.TrySetCanceled(); });

            await Task.WhenAny(task, tcs.Task);
            if (task.IsCompleted)
            {
                return task.Result;
            }
            else
            {
                throw new TaskCanceledException();
            }

        }
    }
}
