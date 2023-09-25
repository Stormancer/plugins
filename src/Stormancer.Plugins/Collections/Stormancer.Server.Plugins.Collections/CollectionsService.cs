using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Stormancer.Abstractions.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
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
            var definitions = JsonConvert.DeserializeObject<CollectableItemDefinitions>(json)?? new CollectableItemDefinitions();

            foreach(var (key,value) in definitions.Items)
            {
                value.Id = key;
            }
            return (definitions.Items, _configSection.CacheDuration);
        }
    }

    /// <summary>
    /// Provides item collection related services.
    /// </summary>
    public interface ICollectionService
    {
        /// <summary>
        /// Unlock a collection item for an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="itemId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UnlockAsync(User user, string itemId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the item collections of a list of users.
        /// </summary>
        /// <param name="userIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, IEnumerable<string>>> GetCollectionAsync(IEnumerable<string> userIds, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the item definitions known by the server.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, CollectableItemDefinition>> GetItemDefinitionsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Resets the collection of a player.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// The operation cannot be undone!
        /// </remarks>
        /// <returns></returns>
        Task ResetAsync(string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Locks an item which was unlocked by the user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="itemId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task LockAsync(string userId,string itemId, CancellationToken cancellationToken);

    }
    internal class CollectionsService : ICollectionService
    {

        private readonly DbContextAccessor _dbContextAccessor;
        private readonly Func<IEnumerable<ICollectionEventHandler>> _eventHandlers;
        private readonly CollectionsRepository _collectionsRepository;

        public CollectionsService(Func<IEnumerable<ICollectionEventHandler>> eventHandlers, CollectionsRepository collectionsRepository, DbContextAccessor dbContextAccessor)
        {
            _eventHandlers = eventHandlers;
            _collectionsRepository = collectionsRepository;
            _dbContextAccessor = dbContextAccessor;
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
            var definitions = await GetItemDefinitionsAsync(cancellationToken);

            if(!definitions.TryGetValue(itemId,out var definition))
            {
                throw new ClientException($"itemNotFound?itemId={itemId}");
            }
            using var dbContext = await _dbContextAccessor.GetDbContextAsync();

            var guid = Guid.Parse(user.Id);
            var item = await dbContext.Set<CollectionItemRecord>().FindAsync(itemId,guid);

            if (item == null)
            {
                var ctx = new UnlockingContext(definition, user);


                await _eventHandlers().RunEventHandler(h => h.OnUnlocking(ctx), ex => { });

                if (ctx.Success)
                {

                    await dbContext.Set<CollectionItemRecord>().AddAsync(new CollectionItemRecord { ItemId =itemId, UserId = guid  });
                }
                else
                {
                    throw new ClientException(ctx.ErrorId??"failedToUnlockItem");
                }

                var unlockedCtx = new UnlockedContext(definition, user,dbContext);
                await _eventHandlers().RunEventHandler(h => h.OnUnlocked(unlockedCtx), ex => { });


                await dbContext.SaveChangesAsync();
            }
        }

        public Task<Dictionary<string,CollectableItemDefinition>> GetItemDefinitionsAsync(CancellationToken cancellationToken)
        {
            return _collectionsRepository.GetDefinitionsAsync(cancellationToken);
        }

        public async Task<Dictionary<string, IEnumerable<string>>> GetCollectionAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            var definitions = await GetItemDefinitionsAsync(cancellationToken);

            var dbContext = await this._dbContextAccessor.GetDbContextAsync("default", cancellationToken);

            var set = dbContext.Set<CollectionItemRecord>();

            var guids = userIds.Select(userId=>Guid.Parse(userId)).ToArray();
            var items = await set.Where(item => guids.Contains(item.User.Id)).Include(item=>item.User).ToListAsync();

            var results = new Dictionary<string, IEnumerable<string>>();

            foreach(var item in items)
            {
                if(!results.TryGetValue(item.User.Id.ToString("N"), out var itemList))
                {
                    itemList = new List<string>();
                    results.Add(item.User.Id.ToString("N"), itemList);
                }

                ((List<string>)itemList).Add(item.ItemId);

            }

            return results;
        }

        public async Task ResetAsync(string userId, CancellationToken cancellationToken)
        {
            var guid = Guid.Parse(userId);
            var dbContext = await this._dbContextAccessor.GetDbContextAsync("default", cancellationToken);

            var set = dbContext.Set<CollectionItemRecord>();
            await set.Where(item => item.UserId == guid).ExecuteDeleteAsync(cancellationToken);

            await dbContext.SaveChangesAsync();
        }

        public async Task LockAsync(string userId, string itemId, CancellationToken cancellationToken)
        {
            var guid = Guid.Parse(userId);
            var dbContext = await this._dbContextAccessor.GetDbContextAsync("default", cancellationToken);

            var set = dbContext.Set<CollectionItemRecord>();
            await set.Where(item => item.UserId == guid && item.ItemId == itemId).ExecuteDeleteAsync(cancellationToken);

            await dbContext.SaveChangesAsync();
        }
    }
}