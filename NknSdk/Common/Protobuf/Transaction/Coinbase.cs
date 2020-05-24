using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Coinbase : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Sender { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Recipient { get; set; }

        [ProtoMember(3)]
        public long Amount { get; set; }
    }
}
