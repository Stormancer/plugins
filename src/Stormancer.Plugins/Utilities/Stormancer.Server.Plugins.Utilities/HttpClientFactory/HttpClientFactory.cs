﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Http;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.HttpClientFactory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace Stormancer.Server.Plugins.Utilities
{



    internal class DefaultHttpMessageHandlerBuilder : HttpMessageHandlerBuilder
    {
        public DefaultHttpMessageHandlerBuilder()
        {

        }

        private string? _name;

        public override string? Name
        {
            get => _name;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _name = value;
            }
        }

        public override HttpMessageHandler PrimaryHandler { get; set; } = new HttpClientHandler();

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();



        public override HttpMessageHandler Build()
        {
            if (PrimaryHandler == null)
            {

                throw new InvalidOperationException($"Primary handler is null");
            }

            return CreateHandlerPipeline(PrimaryHandler, AdditionalHandlers);
        }
    }


    // This a marker used to check if the underlying handler should be disposed. HttpClients
    // share a reference to an instance of this class, and when it goes out of scope the inner handler
    // is eligible to be disposed.
    internal class LifetimeTrackingHttpMessageHandler : DelegatingHandler
    {
        public LifetimeTrackingHttpMessageHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override void Dispose(bool disposing)
        {
            // The lifetime of this is tracked separately by ActiveHandlerTrackingEntry
        }
    }

    internal class DefaultHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
    {

        private static readonly TimerCallback _cleanupCallback = (s) => ((DefaultHttpClientFactory?)s)?.CleanupTimer_Tick();
        private readonly IHttpClientFactoryEventHandler[] handlers;
        private readonly ILogger _logger;

        private readonly IHttpMessageHandlerBuilderFilter[] _filters;
        private readonly Func<string, Lazy<ActiveHandlerTrackingEntry>> _entryFactory;

        // Default time of 10s for cleanup seems reasonable.
        // Quick math:
        // 10 distinct named clients * expiry time >= 1s = approximate cleanup queue of 100 items
        //
        // This seems frequent enough. We also rely on GC occurring to actually trigger disposal.
        private readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromSeconds(10);

        // We use a new timer for each regular cleanup cycle, protected with a lock. Note that this scheme 
        // doesn't give us anything to dispose, as the timer is started/stopped as needed.
        //
        // There's no need for the factory itself to be disposable. If you stop using it, eventually everything will
        // get reclaimed.
        private Timer? _cleanupTimer;
        private readonly object _cleanupTimerLock;
        private readonly object _cleanupActiveLock;

        // Collection of 'active' handlers.
        //
        // Using lazy for synchronization to ensure that only one instance of HttpMessageHandler is created 
        // for each name.
        //
        // internal for tests
        internal readonly ConcurrentDictionary<string, Lazy<ActiveHandlerTrackingEntry>> _activeHandlers;

        // Collection of 'expired' but not yet disposed handlers.
        //
        // Used when we're rotating handlers so that we can dispose HttpMessageHandler instances once they
        // are eligible for garbage collection.
        //
        // internal for tests
        internal readonly ConcurrentQueue<ExpiredHandlerTrackingEntry> _expiredHandlers;
        private readonly TimerCallback _expiryCallback;

        private HttpClientFactoryOptions GetOptions(string name)
        {
            for (int i = 0; i < handlers.Length; i++)
            {
                var opt = handlers[i].GetOptions(name);
                if (opt != null)
                {
                    return opt;
                }
            }
            return new HttpClientFactoryOptions();
        }

        public DefaultHttpClientFactory(
          IEnumerable<IHttpClientFactoryEventHandler> handlers,
            ILogger logger,
            IEnumerable<IHttpMessageHandlerBuilderFilter> filters)
        {


            if (filters == null)
            {
                throw new ArgumentNullException(nameof(filters));
            }


            _filters = filters.ToArray();
            this.handlers = handlers?.ToArray() ?? throw new ArgumentNullException(nameof(handlers));
            _logger = logger;

            // case-sensitive because named options is.
            _activeHandlers = new ConcurrentDictionary<string, Lazy<ActiveHandlerTrackingEntry>>(StringComparer.Ordinal);
            _entryFactory = (name) =>
            {
                return new Lazy<ActiveHandlerTrackingEntry>(() =>
                {
                    return CreateHandlerEntry(name);
                }, LazyThreadSafetyMode.ExecutionAndPublication);
            };

            _expiredHandlers = new ConcurrentQueue<ExpiredHandlerTrackingEntry>();
            _expiryCallback = ExpiryTimer_Tick;

            _cleanupTimerLock = new object();
            _cleanupActiveLock = new object();
        }

        public HttpClient CreateClient(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var handler = CreateHandler(name);
            var client = new HttpClient(handler, disposeHandler: false);

            var options = GetOptions(name);
            for (var i = 0; i < options.HttpClientActions.Count; i++)
            {
                options.HttpClientActions[i](client);
            }

            return client;
        }

        public HttpMessageHandler CreateHandler(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var entry = _activeHandlers.GetOrAdd(name, _entryFactory).Value;

            StartHandlerEntryTimer(entry);

            return entry.Handler;
        }

        // Internal for tests
        internal ActiveHandlerTrackingEntry CreateHandlerEntry(string name)
        {

            //var scope = (IServiceScope)null;

            var options = GetOptions(name);
            //if (!options.SuppressHandlerScope)
            //{
            //    scope = _scopeFactory.CreateScope();
            //    services = scope.ServiceProvider;
            //}

            try
            {
                var builder = new DefaultHttpMessageHandlerBuilder();
                builder.Name = name;

                // This is similar to the initialization pattern in:
                // https://github.com/aspnet/Hosting/blob/e892ed8bbdcd25a0dafc1850033398dc57f65fe1/src/Microsoft.AspNetCore.Hosting/Internal/WebHost.cs#L188
                Action<HttpMessageHandlerBuilder> configure = Configure;
                for (var i = _filters.Length - 1; i >= 0; i--)
                {
                    configure = _filters[i].Configure(configure);
                }

                configure(builder);

                // Wrap the handler so we can ensure the inner handler outlives the outer handler.
                var handler = new LifetimeTrackingHttpMessageHandler(builder.Build());

                // Note that we can't start the timer here. That would introduce a very very subtle race condition
                // with very short expiry times. We need to wait until we've actually handed out the handler once
                // to start the timer.
                // 
                // Otherwise it would be possible that we start the timer here, immediately expire it (very short
                // timer) and then dispose it without ever creating a client. That would be bad. It's unlikely
                // this would happen, but we want to be sure.
                return new ActiveHandlerTrackingEntry(name, handler, options.HandlerLifetime);

                void Configure(HttpMessageHandlerBuilder b)
                {
                    for (var i = 0; i < options.HttpMessageHandlerBuilderActions.Count; i++)
                    {
                        options.HttpMessageHandlerBuilderActions[i](b);
                    }
                }
            }
            catch
            {
                // If something fails while creating the handler, dispose the services.
                //scope?.Dispose();
                throw;
            }
        }

        // Internal for tests
        internal void ExpiryTimer_Tick(object? state)
        {
            var active = (ActiveHandlerTrackingEntry?)state;

            Debug.Assert(active != null, "Callback cannot be called with a null state.");
            

            // The timer callback should be the only one removing from the active collection. If we can't find
            // our entry in the collection, then this is a bug.
            var removed = _activeHandlers.TryRemove(active.Name, out var found);
            Debug.Assert(removed, "Entry not found. We should always be able to remove the entry");
            Debug.Assert(object.ReferenceEquals(active, found!.Value), "Different entry found. The entry should not have been replaced");

            // At this point the handler is no longer 'active' and will not be handed out to any new clients.
            // However we haven't dropped our strong reference to the handler, so we can't yet determine if
            // there are still any other outstanding references (we know there is at least one).
            //
            // We use a different state object to track expired handlers. This allows any other thread that acquired
            // the 'active' entry to use it without safety problems.
            var expired = new ExpiredHandlerTrackingEntry(active);
            _expiredHandlers.Enqueue(expired);


            StartCleanupTimer();
        }

        // Internal so it can be overridden in tests
        internal virtual void StartHandlerEntryTimer(ActiveHandlerTrackingEntry entry)
        {
            entry.StartExpiryTimer(_expiryCallback);
        }

        // Internal so it can be overridden in tests
        internal virtual void StartCleanupTimer()
        {
            lock (_cleanupTimerLock)
            {
                if (_cleanupTimer == null)
                {
                    _cleanupTimer = NonCapturingTimer.Create(_cleanupCallback, this, DefaultCleanupInterval, Timeout.InfiniteTimeSpan);
                }
            }
        }

        // Internal so it can be overridden in tests
        internal virtual void StopCleanupTimer()
        {
            lock (_cleanupTimerLock)
            {
                _cleanupTimer?.Dispose();
                _cleanupTimer = null;
            }
        }

        // Internal for tests
        internal void CleanupTimer_Tick()
        {
            // Stop any pending timers, we'll restart the timer if there's anything left to process after cleanup.
            //
            // With the scheme we're using it's possible we could end up with some redundant cleanup operations.
            // This is expected and fine.
            // 
            // An alternative would be to take a lock during the whole cleanup process. This isn't ideal because it
            // would result in threads executing ExpiryTimer_Tick as they would need to block on cleanup to figure out
            // whether we need to start the timer.
            StopCleanupTimer();

            if (!Monitor.TryEnter(_cleanupActiveLock))
            {
                // We don't want to run a concurrent cleanup cycle. This can happen if the cleanup cycle takes
                // a long time for some reason. Since we're running user code inside Dispose, it's definitely
                // possible.
                //
                // If we end up in that position, just make sure the timer gets started again. It should be cheap
                // to run a 'no-op' cleanup.
                StartCleanupTimer();
                return;
            }

            try
            {
                var initialCount = _expiredHandlers.Count;
                _logger.Log(LogLevel.Trace, "httpClientFactory", "Cleanup cycle start.", new { });



                var disposedCount = 0;
                for (var i = 0; i < initialCount; i++)
                {
                    // Since we're the only one removing from _expired, TryDequeue must always succeed.
                    _expiredHandlers.TryDequeue(out var entry);
                    Debug.Assert(entry != null, "Entry was null, we should always get an entry back from TryDequeue");

                    if (entry.CanDispose)
                    {
                        try
                        {
                            entry.InnerHandler.Dispose();
                            //entry.Scope?.Dispose();
                            disposedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(LogLevel.Error, "httpClientFactory", "Cleanup failed.", ex);

                        }
                    }
                    else
                    {
                        // If the entry is still live, put it back in the queue so we can process it 
                        // during the next cleanup cycle.
                        _expiredHandlers.Enqueue(entry);
                    }
                }
                _logger.Log(LogLevel.Trace, "httpClientFactory", "Cleanup cycle complete.", new { });
            }
            finally
            {
                Monitor.Exit(_cleanupActiveLock);
            }

            // We didn't totally empty the cleanup queue, try again later.
            if (_expiredHandlers.Count > 0)
            {
                StartCleanupTimer();
            }
        }


    }
}
