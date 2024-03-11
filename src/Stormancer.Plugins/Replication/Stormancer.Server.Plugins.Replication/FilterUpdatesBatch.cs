
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.Replication
{
    /// <summary>
    /// Contains data relative to a view update.
    /// </summary>
    [MessagePackObject]
    public class FilterUpdatesBatch
    {
        [Key(0)]
        public List<FilterUpdate> Updates { get; set; } = new List<FilterUpdate>();
    }

    
    public enum UpdateType
    {
        Remove,
        AddOrUpdate
    }

    [MessagePackObject]
    public class FilterUpdate
    {
        [Key(0)]
        public UpdateType UpdateType { get; set; }

        [Key(1)]
        public SessionId Owner { get; set; }

        [Key(2)]
        public required string ViewId { get; set; }

        [Key(3)]
        public byte[] FilterData { get; set; } = Array.Empty<byte>();

        [Key(4)]
        public bool IsAuthority { get; set; }

        [Key(5)]
        public required string ViewPolicyId { get; set; }

        [Key(6)]
        public required string FilterType { get; set; }
    }
}
