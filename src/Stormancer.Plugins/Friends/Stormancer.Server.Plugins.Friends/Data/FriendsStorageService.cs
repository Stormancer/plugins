using Lucene.Net.Codecs.Compressing;
using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends.Data
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="UserId">Id of the user representing the Member</param>
    /// <param name="ListOwnerId"></param>
    public record struct MemberId(Guid UserId, Guid ListOwnerId)
    {
    }

    /// <summary>
    /// A record representing an entry in a friend list, a guild member or anything similar.
    /// </summary>
    [PrimaryKey(nameof(FriendId), nameof(OwnerId))]
    public class MemberRecord : IDisposable
    {
        /// <summary>
        /// Creates a new <see cref="MemberRecord"/>.
        /// </summary>
        /// <param name="memberId"></param>
        /// <param name="status"></param>
        /// <param name="listType"></param>
        [SetsRequiredMembers]
        public MemberRecord(MemberId memberId, MemberRecordStatus status,string listType)
        {
            FriendId = memberId.UserId;
            OwnerId = memberId.ListOwnerId;
            Status = status;
            CustomData = JsonDocument.Parse("{}");
            ListType = listType;
        }
        /// <summary>
        /// Creates a new <see cref="MemberRecord"/>
        /// </summary>
        public MemberRecord()
        {
        }

        /// <summary>
        /// Creates a new <see cref="MemberRecord"/> from a dto.
        /// </summary>
        public MemberRecord(MemberDto dto)
        {
            FriendId = Guid.Parse(dto.FriendId);
            OwnerId = Guid.Parse(dto.OwnerId);
            Tags = dto.Tags;
            Status = dto.Status;
            Tags = dto.Tags;
            Expiration = dto.Expiration;
        }

        /// <summary>
        /// Gets or sets the list type of this record (Friend list, org, etc...)
        /// </summary>
        public required string ListType { get; set; }

        /// <summary>
        /// Gets or sets the id of the friend.
        /// </summary>
        public required Guid FriendId { get; set; }

        /// <summary>
        /// Gets or sets the friend.
        /// </summary>
        [Required]
        [ForeignKey(nameof(FriendId))]
        public UserRecord Friend { get; set; } = default!;

        /// <summary>
        /// Gets or sets the id of the entity owning the item.
        /// </summary>
        public required Guid OwnerId { get; set; }

        /// <summary>
        /// Roles associated with the entry. (for instance BLOCKED)
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Invitation status.
        /// </summary>
        public required MemberRecordStatus Status { get; set; }

        /// <summary>
        /// List of 
        /// </summary>
        [Column(TypeName = "jsonb")]
        public required JsonDocument CustomData { get; set; }

        /// <summary>
        /// Expiration of the record.
        /// </summary>
        public DateTime? Expiration { get; set; }

        ///<inheritdoc/>
        public void Dispose()
        {
            CustomData.Dispose();
        }
    }

    /// <summary>
    /// Type of operation on a member.
    /// </summary>
    public enum MembersOperationType
    {
        /// <summary>
        /// Adds the member.
        /// </summary>
        Add,
        /// <summary>
        /// Removes the member.
        /// </summary>
        Update,

        /// <summary>
        /// Updates the member.
        /// </summary>
        Delete
    }

    /// <summary>
    /// An operation on a member.
    /// </summary>
    /// <param name="Type"></param>
    /// <param name="Record"></param>
    /// <param name="Id"></param>
    /// <param name="Updater"></param>
    public record class MembersOperation(MembersOperationType Type, MemberRecord? Record, MemberId Id, Action<MemberRecord>  Updater)
    {
    }

    /// <summary>
    /// Builds a batch of member operations.
    /// </summary>
    public class MembersOperationsBuilder
    {
        /// <summary>
        /// Members already known.
        /// </summary>
        public Dictionary<MemberId, MemberRecord> KnownMembers { get; } = new();
        /// <summary>
        /// List of operations in this batch.
        /// </summary>
        public List<MembersOperation> Operations = new();

        /// <summary>
        /// Creates a new <see cref="MembersOperationsBuilder"/>.
        /// </summary>
        public MembersOperationsBuilder(params MemberRecord?[] members)
        {
            foreach (var member in members)
            {
                if (member != null)
                {
                    KnownMembers[new MemberId(member.FriendId, member.OwnerId)] = member;
                }
            }
        }

        /// <summary>
        /// Adds a record.
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public MembersOperationsBuilder Add(MemberRecord member)
        {
            KnownMembers[new MemberId(member.FriendId,member.OwnerId)] = member;
            Operations.Add(new(MembersOperationType.Add, member, new (member.FriendId,member.OwnerId), _ => { }));
            return this;
        }

        /// <summary>
        /// Updates an existing record.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updater"></param>
        /// <returns></returns>
        public MembersOperationsBuilder Update(MemberId id, Action<MemberRecord> updater)
        {
            Operations.Add(new(MembersOperationType.Update, null,id,updater));
            return this;
        }

        /// <summary>
        /// Deletes a record
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public MembersOperationsBuilder Delete(MemberRecord member)
        {
            Operations.Add(new(MembersOperationType.Delete, member, new(member.FriendId, member.OwnerId), _ => { }));
            return this;
        }

    }

    internal class MembersStorageService
    {
        private readonly DbContextAccessor _contextAccessor;

        public MembersStorageService(DbContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        public async Task SaveBatchAsync(MembersOperationsBuilder builder)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();
            var set = ctx.Set<MemberRecord>();
            foreach (var operation in builder.Operations)
            {
                switch (operation.Type)
                {
                    case MembersOperationType.Add:
                        Debug.Assert(operation.Record != null);
                        set.Add(operation.Record);
                        break;
                    case MembersOperationType.Update:
                        var record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                        Debug.Assert(record != null);
                        operation.Updater(record);
                        break;
                    case MembersOperationType.Delete:
                        record = builder.KnownMembers[new MemberId(operation.Id.UserId, operation.Id.ListOwnerId)];
                        Debug.Assert(record != null);
                        set.Remove(record);
                        break;
                }
            }
            await ctx.SaveChangesAsync();
            
        }
        
        public async Task<bool> IsUserInMemberListAsync(Guid friendId, Guid ownerId)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();

            var member = await ctx.Set<MemberRecord>().FindAsync(friendId, ownerId);
            return member != null;
        }

        public async Task RemoveMembersAsync(IEnumerable<MemberId> memberIds)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();

            foreach (var memberId in memberIds)
            {
                var member = await ctx.Set<MemberRecord>().FindAsync(memberId.UserId, memberId.ListOwnerId);
                if (member != null)
                {
                    ctx.Set<MemberRecord>().Remove(member);
                }
            }
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(IEnumerable<(MemberId memberId, MemberRecordStatus newStatus)> updates)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();
            var set = ctx.Set<MemberRecord>();
            foreach (var (memberId, newStatus) in updates)
            {
                var member = await set.FindAsync(memberId.UserId, memberId.ListOwnerId);
                if (member != null)
                {
                    member.Status = newStatus;
                    set.Update(member);

                }
            }
            await ctx.SaveChangesAsync();
        }

        public async Task<MemberRecord?> GetListMemberAsync(MemberId memberId)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();
            var set = ctx.Set<MemberRecord>();

            return await set.FindAsync(memberId.UserId, memberId.ListOwnerId);

        }

        public async Task<IEnumerable<MemberRecord>> GetListsContainingMemberAsync(Guid memberId, bool onlyAccepted,string listType)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();
            var set = ctx.Set<MemberRecord>();
            if(onlyAccepted)
            {
                return await set.Where(m => m.ListType == listType && m.Status == MemberRecordStatus.Accepted && m.FriendId == memberId).ToListAsync();
            }
            else
            {
                return await set.Where(m => m.ListType == listType  && m.FriendId == memberId).ToListAsync();
            }
        }

        public async Task<IEnumerable<MemberRecord>> GetListMembersAsync(Guid listOwnerId)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();
            var set = ctx.Set<MemberRecord>();

            return await set.Where(m=>m.OwnerId == listOwnerId).ToListAsync();
        }

        public async Task<IEnumerable<MemberRecord>> GetListMembersAsync(IEnumerable<Guid> ownerIds, MemberRecordStatus status)
        {
            var ctx = await _contextAccessor.GetDbContextAsync();
            var set = ctx.Set<MemberRecord>();
            var ownerIdsList = ownerIds.ToList();
            var list = await set.Where(m => ownerIdsList.Contains(m.OwnerId) && m.Status == status).ToListAsync();

            
            foreach(var member in list)
            {
                if(member.Expiration < DateTime.UtcNow)
                {
                    set.Remove(member);
                }
            }

            await ctx.SaveChangesAsync();
            return list;
        }
    }

    internal class FriendsDbModelBuilder : IDbModelBuilder
    {
        public void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData)
        {

            modelBuilder.Entity<MemberRecord>().HasIndex(x => new { x.OwnerId });
            modelBuilder.Entity<MemberRecord>().HasIndex(x => new { x.FriendId });

        }
    }
}
