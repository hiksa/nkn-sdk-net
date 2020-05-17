using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Program
    {
        [ProtoMember(1)]
        public byte[] Code { get; set; }

        [ProtoMember(2)]
        public byte[] Parameter { get; set; }
    }
}
