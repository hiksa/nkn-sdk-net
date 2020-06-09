using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class UnsignedTransaction : ISerializable
    {
        [ProtoMember(1)]
        public Payload Payload { get; set; }

        [ProtoMember(2)]
        public ulong Nonce { get; set; }

        [ProtoMember(3)]
        public long Fee { get; set; }

        [ProtoMember(4, IsPacked = true)]
        public byte[] Attributes { get; set; }
    }
}
