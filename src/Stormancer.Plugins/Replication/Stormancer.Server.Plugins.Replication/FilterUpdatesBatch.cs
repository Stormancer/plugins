using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.Replication
{
    /// <summary>
    /// Contains data relative to a view update.
    /// </summary>
    public class FilterUpdatesBatch
    {
        [MessagePackMember(0)]
        public List<FilterUpdate> Updates { get; set; } = new List<FilterUpdate>();
    }

    [MessagePackEnum(SerializationMethod = EnumSerializationMethod.ByUnderlyingValue)]
    public enum UpdateType
    {
        Remove,
        AddOrUpdate
    }

    public class FilterUpdate
    {
        [MessagePackMember(0)]
        public UpdateType UpdateType { get; set; }

        [MessagePackMember(1)]
        public SessionId Owner { get; set; }

        [MessagePackMember(2)]
        public string ViewId { get; set; } = default!;

        [MessagePackMember(3)]
        public byte[] FilterData { get; set; } = default!;

        [MessagePackMember(4)]
        public bool IsAuthority { get; set; }

        [MessagePackMember(5)]
        public string ViewPolicyId { get; set; }

        [MessagePackMember(6)]
        public string FilterType { get; set; }
    }
}
