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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins
{

    /// <summary>
    /// Represents a simple memory cache.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    public class MemoryCache<TKey, T> : IDisposable where T : class where TKey : notnull
    {
        private class CacheEntry
        {
            public CacheEntry(TKey id, Task<T?> content, DateTime expiresOn, Action<TKey> onInvalidated)
            {
                async Task<T?> GetContent(Task<T?> c, Action<TKey> invalidate)
                {
                    try
                    {
                        var r = await c;
                        if (r == null)
                        {
                            invalidate(id);
                        }
                        else
                        {
                            ExpiresOn = expiresOn;
                        }
                        return r;
                    }
                    catch (Exception)
                    {
                        invalidate(id);
                        throw;
                    }
                }
                ExpiresOn = null;
                Content = GetContent(content, onInvalidated);
                CreatedOn = DateTime.UtcNow;

                Id = id;
                OnInvalidated = onInvalidated;
            }

            public CacheEntry(TKey id, Task<(T?, TimeSpan)> content, Action<TKey> onInvalidated)
            {
                async Task<T?> GetContent(Task<(T?, TimeSpan)> c, Action<TKey> invalidate)
                {
                    try
                    {
                        var (r, invalidationDelay) = await c;
                        if (r == null)
                        {
                            invalidate(id);
                        }
                        else
                        {
                            ExpiresOn = DateTime.UtcNow + invalidationDelay;
                        }
                        return r;
                    }
                    catch (Exception)
                    {
                        invalidate(id);
                        throw;
                    }
                }
                ExpiresOn = null;
                Content = GetContent(content, onInvalidated);
                CreatedOn = DateTime.UtcNow;

                Id = id;
                OnInvalidated = onInvalidated;
            }

            public Task<T?> Content { get; }
            public DateTime CreatedOn { get; }
            public DateTime? ExpiresOn { get; private set; }
            public TKey Id { get; }
            public Action<TKey> OnInvalidated { get; }
        }

        private object _syncRoot = new object();

        Dictionary<TKey, CacheEntry> cache = new Dictionary<TKey, CacheEntry>();
        private PeriodicTimer? _timer;
        private bool _disposed = false;

        /// <summary>
        /// Creates the memory cache.
        /// </summary>
        public MemoryCache()
        {

            
        }

        private void TryStartCleaner()
        {
            if (_timer == null && !_disposed)
            {
                lock (_syncRoot)
                {
                    if (_timer == null && !_disposed)
                    {
                        _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
                        _ = RunCleaner();
                    }
                }
            }
        }
        private async Task RunCleaner()
        {
            while (_timer != null && await _timer.WaitForNextTickAsync())
            {
                KeyValuePair<TKey, CacheEntry>[] copy;
                lock (_syncRoot)
                {
                    copy = cache.Where(kvp => (kvp.Value.ExpiresOn ?? DateTime.MaxValue) < DateTime.UtcNow).ToArray();
                }

                foreach (var entry in copy)
                {
                    entry.Value.OnInvalidated(entry.Key);
                }

                lock (_syncRoot)
                {
                    if (!cache.Any())
                    {
                        _timer?.Dispose();
                        _timer = null;
                    }
                }
            }

        }

        /// <summary>
        /// Gets from cache or update the cache if entry not found or stale. 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="addFunction"></param>
        /// <param name="invalidationDelay"></param>
        /// <returns></returns>
        public Task<T?> Get(TKey id, Func<TKey, Task<T?>> addFunction, TimeSpan invalidationDelay)
        {
            return Get(id, static async (key, tuple) =>
            {
                var (addFunction, invalidationDelay) = tuple;
                return (await addFunction(key), invalidationDelay);
            }, (addFunction, invalidationDelay));
        }

        /// <summary>
        /// Tries to peek a value in the cache, and returns false if the value doesn't exist.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <param name="expiresOn">Expiration date of the entry. Can be null if the task didn't complete yet.</param>
        /// <returns></returns>
        public bool TryPeek(TKey id, [NotNullWhen(true)] out Task<T?>? value, out DateTime? expiresOn)
        {
            lock (_syncRoot)
            {
                if (cache.TryGetValue(id, out var entry) && entry.ExpiresOn != null && entry.ExpiresOn >= DateTime.UtcNow)
                {
                    expiresOn = entry.ExpiresOn;
                    value = entry.Content;
                    return true;
                }
                else
                {
                    expiresOn = default;
                    value = default;
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets an entry in the cache or adds it if necessary
        /// </summary>
        /// <param name="id"></param>
        /// <param name="addFunction"></param>
        /// <returns></returns>
        public async Task<T?> Get(TKey id, Func<TKey, Task<(T?, TimeSpan)>> addFunction)
        {
            CacheEntry? entry;
            lock (_syncRoot)
            {
                if (!cache.TryGetValue(id, out entry) || entry.ExpiresOn == null || entry.ExpiresOn < DateTime.UtcNow)
                {
                    entry = new CacheEntry(id, addFunction(id), (i) => Remove(i));
                    cache[id] = entry;
                    TryStartCleaner();
                }
            }

            return await entry.Content;
        }
        /// <summary>
        /// Gets from the cache or update a cached value.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="addFunction"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public Task<T?> Get<TState>(TKey id, Func<TKey, TState, Task<(T?, TimeSpan)>> addFunction, TState state)
        {
            CacheEntry? entry;
            lock (_syncRoot)
            {
                if (!cache.TryGetValue(id, out entry) || entry.ExpiresOn == null || entry.ExpiresOn < DateTime.UtcNow)
                {
                    entry = new CacheEntry(id, addFunction(id, state), (i) => Remove(i));
                    cache[id] = entry;
                    TryStartCleaner();
                }
            }

            return entry.Content;
        }

        /// <summary>
        /// Gets several cache values at once, and calls 'addFunction' once for all unknown values, enabling batch retrieval.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="addFunction"></param>
        /// <returns></returns>
        public Dictionary<TKey, Task<T?>> GetMany(IEnumerable<TKey> ids, Func<IEnumerable<TKey>, Dictionary<TKey, Task<(T?, TimeSpan)>>> addFunction)
        {
            var results = new Dictionary<TKey, Task<T?>>();
            lock (_syncRoot)
            {
                var unknownIds = new List<TKey>();
                foreach (var id in ids)
                {
                    if (cache.TryGetValue(id, out var entry) && entry.ExpiresOn != null && entry.ExpiresOn >= DateTime.UtcNow)
                    {
                        results.Add(id, entry.Content);
                    }
                    else
                    {
                        unknownIds.Add(id);
                    }
                }
                if (unknownIds.Any())
                {
                    foreach (var r in addFunction(unknownIds))
                    {
                        var entry = new CacheEntry(r.Key, r.Value, (i) => Remove(i));
                        cache[r.Key] = entry;
                        TryStartCleaner();
                        results.Add(r.Key, entry.Content);
                    }
                }


            }
            return results;
        }


        /// <summary>
        /// Removes an entry from the cache.
        /// </summary>
        /// <param name="id"></param>
        public void Remove(TKey id)
        {
            lock (_syncRoot)
            {
                cache.Remove(id, out _);
            }
        }


        /// <summary>
        /// Disposes the cache.
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                _disposed = true;

                _timer?.Dispose();
            }
        }
    }
}
