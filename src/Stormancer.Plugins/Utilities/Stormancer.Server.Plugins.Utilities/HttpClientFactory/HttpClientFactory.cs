// Copyright (c) Vijay Prakash. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Arnath.StandaloneHttpClientFactory
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Factory for <see cref="HttpClient"/> instances that follows the recommended
    /// best practices for creation and disposal. See <a href="https://github.com/arnath/standalonehttpclientfactory" />
    /// for more details.
    /// </summary>
    internal class StandaloneHttpClientFactory : IHttpClientFactory, IDisposable
    {
        internal static readonly TimeSpan DefaultConnectionLifetime = TimeSpan.FromMinutes(15);

        private readonly Dictionary<string, IHttpClientFactory> frameworkSpecificFactories = new Dictionary<string, IHttpClientFactory>();
        private readonly object _syncRoot = new object();
        private readonly TimeSpan connectionLifetime;
        private readonly DelegatingHandler[] delegatingHandlers;

        /// <summary>
        /// Creates a new instance of the StandaloneHttpClientFactory class with the
        /// default value of 15 mins for pooled connection lifetime and no delegating
        /// handlers.
        /// </summary>
        public StandaloneHttpClientFactory()
            : this(DefaultConnectionLifetime, delegatingHandlers: null)
        {
        }



     

        /// <summary>
        /// Creates an instance of the StandaloneHttpClientFactory class with the
        /// specified value for pooled connection lifetime and the specified
        /// set of delegating handlers. The delegating handlers will be run in order.
        /// </summary>
        /// <param name="connectionLifetime">The lifetime of connections to each host.</param>
        /// <param name="delegatingHandlers">Array of DelegatingHandler instances that can be
        /// used for logging, etc. See LoggingHttpMessageHandler for an example.</param>
        public StandaloneHttpClientFactory(TimeSpan connectionLifetime,params DelegatingHandler[] delegatingHandlers)
        {
            this.connectionLifetime = connectionLifetime;
            this.delegatingHandlers = delegatingHandlers;
            //this.frameworkSpecificFactory = new DotNetCoreHttpClientFactory(connectionLifetime, delegatingHandlers);

        }

        /// <summary>
        /// Creates an HTTP client instance with the factory's pooled connection lifetime
        /// and delegating handlers.
        /// </summary>
        /// <returns>The HTTP client instance. This can be disposed freely; the instances
        /// returned by the factory will handle doing the right thing.</returns>
        public HttpClient CreateClient(string name)
        {
            lock(_syncRoot)
            {
                if(!frameworkSpecificFactories.TryGetValue(name,out var factory))
                {
                    factory = new DotNetCoreHttpClientFactory(connectionLifetime, delegatingHandlers);
                    frameworkSpecificFactories.Add(name, factory);
                }
                return factory.CreateClient(name);
            }
        }

        /// <summary>
        /// Releases the resources used by the StandaloneHttpClientFactory. Does nothing
        /// when called from .NET Core.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Linsk together a set of DelegatingHandler instances with the framework specific
        /// handler to generate a handler pipeline that can be used to do things like log
        /// request and response information. The delegating handlers will be run in order.
        /// </summary>
        private static HttpMessageHandler CreateHandlerPipeline(
            HttpMessageHandler handler,
            params DelegatingHandler[] delegatingHandlers)
        {
            HttpMessageHandler next = handler;
            if (delegatingHandlers != null)
            {
                for (int i = delegatingHandlers.Length - 1; i >= 0; i--)
                {
                    delegatingHandlers[i].InnerHandler = next;
                    next = delegatingHandlers[i];
                }
            }

            return next;
        }

        protected virtual void Dispose(bool disposing)
        {
           
        }


        /// <summary>
        /// .NET Core version of the HTTP client factory. This uses a single
        /// shared instance of SocketsHttpHandler with a pooled connection
        /// lifetime set and generates new HttpClient instances on every request.
        /// </summary>
        private class DotNetCoreHttpClientFactory : IHttpClientFactory
        {
            private readonly Lazy<HttpMessageHandler> lazyHandler;

            internal DotNetCoreHttpClientFactory(TimeSpan pooledConnectionLifetime,
                params DelegatingHandler[] delegatingHandlers)
            {
                this.lazyHandler = new Lazy<HttpMessageHandler>(
                    () => CreateLazyHandler(pooledConnectionLifetime, delegatingHandlers),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(this.lazyHandler.Value, disposeHandler: false);
            }

            private static HttpMessageHandler CreateLazyHandler(
                TimeSpan pooledConnectionLifetime,
                params DelegatingHandler[] delegatingHandlers)
            {
                SocketsHttpHandler handler = new SocketsHttpHandler();
                handler.PooledConnectionLifetime = pooledConnectionLifetime;

                return CreateHandlerPipeline(handler, delegatingHandlers);
            }
        }

    }
}