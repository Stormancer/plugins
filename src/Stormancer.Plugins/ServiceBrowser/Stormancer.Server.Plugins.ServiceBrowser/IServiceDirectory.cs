using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.ServiceBrowser
{
    /// <summary>
    /// Provides APIs to search and reserve connection slots to services.
    /// </summary>
    public interface IServiceDirectory
    {
        /// <summary>
        /// Searches for active services of the provided type in the application.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<ServiceReference> SearchAsync(string type, CancellationToken cancellationToken);

        /// <summary>
        /// Reserve a connection slot in the service.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="customData"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A reservation object. Must be disposed to release.</returns>
        Task<ServiceConnectionLease> AcquireConnectionLeaseAsync(ServiceReference service, JObject customData ,CancellationToken cancellationToken);

        Task<ServiceConnectionLease> RenewLeaseAsync(ServiceConnectionLease currentLease, JObject? customData, CancellationToken cancellationToken);

        Task CancelLeasAsync(ServiceConnectionLease lease, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A connection reservation to 
    /// </summary>
    public class ServiceConnectionLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Reference to a service provided by <see cref="IServiceDirectory"/>
    /// </summary>
    public class ServiceReference
    {

    }
}
