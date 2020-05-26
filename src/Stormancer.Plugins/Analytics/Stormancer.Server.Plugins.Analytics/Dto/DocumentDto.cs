
using MsgPack.Serialization;

namespace Stormancer.Server.Plugins.Analytics
{
    public class DocumentDto
    {
        [MessagePackMember(0)]
        public string Type { get; set; }
       
        [MessagePackMember(1)]
        public string Content;

        [MessagePackMember(2)]
        public string Category { get; set; }
    }
}
