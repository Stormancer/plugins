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
    public class MemoryCache<T> : IDisposable where T : class
    {
        private class CacheEntry
        {
            public CacheEntry(string id, Task<T?> content, DateTime expiresOn, Action<string> onInvalidated)
            {
                async Task<T?> GetContent(Task<T?> c, Action<string> invalidate)
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

            public CacheEntry(string id, Task<(T?, TimeSpan)> content, Action<string> onInvalidated)
            {
                async Task<T?> GetContent(Task<(T?, TimeSpan)> c, Action<string> invalidate)
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
            public string Id { get; }
            public Action<string> OnInvalidated { get; }
        }

        private object _syncRoot = new object();

        Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
      
        private PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        /// <summary>
        /// Creates the memory cache.
        /// </summary>
        public MemoryCache()
        {
            Task.Run(async () =>
            {

                while (await _timer.WaitForNextTickAsync())
                {


                    foreach (var entry in cache.Where(kvp => (kvp.Value.ExpiresOn ?? DateTime.MaxValue) < DateTime.UtcNow).ToArray())
                    {
                        entry.Value.OnInvalidated(entry.Key);
                    }
                   ;

                }
            });
        }

        /// <summary>
        /// Gets from cache or update the cache if entry not found or stale. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="addFunction"></param>
        /// <param name="invalidationDelay"></param>
        /// <returns></returns>
        public async Task<T?> Get(string id, Func<string, Task<T?>> addFunction, TimeSpan invalidationDelay)
        {
            CacheEntry? entry;
            lock (_syncRoot)
            {
                if (!cache.TryGetValue(id, out entry))
                {
                    entry = new CacheEntry(id, addFunction(id), DateTime.UtcNow + invalidationDelay, (i) => Remove(i));
                    cache.Add(id, entry);
                }
            }

            return await entry.Content;
        }

        /// <summary>
        /// Tries to peek a value in the cache, and returns false if the value doesn't exist.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <param name="expiresOn">Date d'expiration de l'entrée. Can be null if the task didn't complete yet.</param>
        /// <returns></returns>
        public bool TryPeek(string id, [NotNullWhen(true)] out Task<T?>? value, out DateTime? expiresOn)
        {
            lock (_syncRoot)
            {
                if (cache.TryGetValue(id, out var entry) && (entry.ExpiresOn == null || entry.ExpiresOn > DateTime.UtcNow))
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
        /// Gets from the cache or update a cached value.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="addFunction"></param>
        /// <returns></returns>
        public async Task<T?> Get(string id, Func<string, Task<(T?, TimeSpan)>> addFunction)
        {
            CacheEntry? entry;
            lock (_syncRoot)
            {
                if (!cache.TryGetValue(id, out entry) || (entry.ExpiresOn != null && entry.ExpiresOn < DateTime.UtcNow))
                {
                    entry = new CacheEntry(id, addFunction(id), (i) => Remove(i));
                    cache[id] = entry;
                }
            }

            return await entry.Content;
        }

        /// <summary>
        /// Gets several cache values at once, and calls 'addFunction' once for all unknown values, enabling batch retrieval.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="addFunction"></param>
        /// <returns></returns>
        public Dictionary<string, Task<T?>> GetMany(IEnumerable<string> ids, Func<IEnumerable<string>, Dictionary<string, Task<(T?, TimeSpan)>>> addFunction)
        {
            var results = new Dictionary<string, Task<T?>>();
            lock (_syncRoot)
            {
                var unknownIds = new List<string>();
                foreach (var id in ids)
                {
                    if (cache.TryGetValue(id, out var entry) && (entry.ExpiresOn == null || entry.ExpiresOn > DateTime.UtcNow))
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
        public void Remove(string id)
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
            _timer.Dispose();
        }
    }
}
