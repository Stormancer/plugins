using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    /// <summary>
    /// Dependencies registered with this contract can participate in the processes associated with the collection system.
    /// </summary>
    public interface ICollectionEventHandler
    {
        /// <summary>
        /// Fired when a player is trying to unlock a collectable item.
        /// </summary>
        /// <param name="context"></param>
        /// <remarks>
        /// Set Success to false to cancel the unlocking process.
        /// </remarks>
        /// <returns></returns>
        public Task OnUnlocking(UnlockingContext context);

        /// <summary>
        /// Fired when unlocking has been accepted.
        /// </summary>
        /// <param name="context"></param>
        /// <remarks>Changes performed on the database using <see cref="UnlockedContext.DbContext"/> are part of the unlocking transaction.</remarks>
        /// <returns></returns>
        public Task OnUnlocked(UnlockedContext context);
    }

    /// <summary>
    /// Context for <see cref="ICollectionEventHandler.OnUnlocked(UnlockedContext)"/>
    /// </summary>
    public class UnlockedContext
    {
        internal UnlockedContext(CollectableItemDefinition item, User user,AppDbContext appDbContext)
        {
            Item = item;
            User = user;
            DbContext = appDbContext;
        }


        /// <summary>
        /// Gets the item the player is unlocking
        /// </summary>
        public CollectableItemDefinition Item { get; }

        /// <summary>
        /// User performing the action.
        /// </summary>
        public User User { get; }

        /// <summary>
        /// Gets the Database context to use to perform any updates associated with the operation.
        /// </summary>
        public AppDbContext DbContext { get; }
    }
    /// <summary>
    /// Context passed to <see cref="ICollectionEventHandler.OnUnlocking"/>
    /// </summary>
    public class UnlockingContext
    {
        
        internal UnlockingContext(CollectableItemDefinition definition, User user)
        {
            Item = definition;
            User = user;
        }
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Gets or sets the id of the error that should be sent back to the game client.
        /// </summary>
        public string? ErrorId { get; set; }

        /// <summary>
        /// Gets the item the player is unlocking
        /// </summary>
        public CollectableItemDefinition Item { get; }

        /// <summary>
        /// User performing the action.
        /// </summary>
        public User User { get; }
    }
}
