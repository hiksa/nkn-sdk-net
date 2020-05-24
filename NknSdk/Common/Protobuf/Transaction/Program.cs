using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Program : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Code { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Parameter { get; set; }
    }
}
