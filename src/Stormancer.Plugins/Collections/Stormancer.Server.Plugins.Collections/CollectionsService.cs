using Newtonsoft.Json;
using Stormancer.Abstractions.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    internal class CollectionsRepository : IConfigurationChangedEventHandler
    {
        private readonly IConfiguration _configuration;
        private readonly IBlobStore _blobStore;
        private MemoryCache<string,Dictionary<string,CollectableItemDefinition>> _itemDefinitions = new Plugins.MemoryCache<string, Dictionary<string,CollectableItemDefinition>>();
        private CollectionConfigSection _configSection;

        public CollectionsRepository(IConfiguration configuration, IBlobStore blobStore)
        {
            _configuration = configuration;
            _blobStore = blobStore;
            OnConfigurationChanged();
        }

        [MemberNotNull("_configSection")]
        public void OnConfigurationChanged()
        {
            var section = _configuration.GetValue(CollectionConfigSection.SECTION_PATH, new CollectionConfigSection());
            var currentItemDefinitionPath = _configSection?.ItemDefinitionsPath;
            if (currentItemDefinitionPath != null)
            {
                _itemDefinitions.Remove(currentItemDefinitionPath);
            }
            _configSection = section;
            
        }

        public async Task<Dictionary<string,CollectableItemDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        {
            var currentItemDefinitionPath = _configSection?.ItemDefinitionsPath;
            if (currentItemDefinitionPath == null)
            {
                throw new InvalidOperationException($"itemDefinitionsNotConfigured");
            }

            var result = await _itemDefinitions.Get(currentItemDefinitionPath,path=>  GetDefinitionsImplAsync(path,cancellationToken));
            if(result == null)
            {
                throw new InvalidOperationException("itemDefinitionsMissing");
            }
            return result;
        }

        private async Task<(Dictionary<string,CollectableItemDefinition>? itemDefinitions, TimeSpan cacheTimeout)> GetDefinitionsImplAsync(string path, CancellationToken cancellationToken)
        {
            var firstSegmentEndIndex = path.IndexOf('/');
            if(firstSegmentEndIndex < 0)
            {
                throw new InvalidOperationException($"Invalid Item definitions blob path (${path})");
            }
            var blob = await _blobStore.GetBlobContentAsync(path.Substring(0, firstSegmentEndIndex), path.Substring(firstSegmentEndIndex + 1, path.Length - firstSegmentEndIndex - 1),cancellationToken) ;


            if(blob == null)
            {
                throw new InvalidOperationException("itemDefinitionsMissing?reason=blobNotFound");
            }
            var readResult = await blob.Reader.ReadAtLeastAsync(10 * 1024 * 1024);
            var json =Encoding.UTF8.GetString(readResult.Buffer);
            return (JsonConvert.DeserializeObject<Dictionary<string,CollectableItemDefinition>>(json), _configSection.CacheDuration);
        }
    }
    internal class CollectionsService
    {
        private readonly Func<IEnumerable<ICollectionEventHandler>> _eventHandlers;
        private readonly CollectionsRepository _collectionsRepository;

        public CollectionsService(Func<IEnumerable<ICollectionEventHandler>> eventHandlers, CollectionsRepository collectionsRepository)
        {
            _eventHandlers = eventHandlers;
            _collectionsRepository = collectionsRepository;
        }

        /// <summary>
        /// Unlocks an item for an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="itemId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task UnlockAsync(User user, string itemId, CancellationToken cancellationToken)
        {
            var definitions = await GetItemDefinitionAsync(cancellationToken);

            if(!definitions.TryGetValue(itemId,out var definition))
            {
                throw new ClientException($"itemNotFound?itemId={itemId}");
            }

            var ctx = new UnlockingContext(definition, user);

            await _eventHandlers().RunEventHandler(h => h.OnUnlocking(ctx), ex => { });

           if(ctx.Success)
           {

           }

            await _eventHandlers().RunEventHandler(h => h.OnUnlocked(ctx), ex => { });
        }

        public Task<Dictionary<string,CollectableItemDefinition>> GetItemDefinitionAsync(CancellationToken cancellationToken)
        {
            return _collectionsRepository.GetDefinitionsAsync(cancellationToken);
        }

        public async Task<Dictionary<string, IEnumerable<string>>> GetCollectionAsync(IEnumerable<string> userIds, CancellationToken CancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}