using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class NanoPay : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Sender { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Recipient { get; set; }

        [ProtoMember(3)]
        public ulong Id { get; set; }

        [ProtoMember(4)]
        public long Amount { get; set; }

        [ProtoMember(5)]
        public int TransactionExpiration { get; set; }

        [ProtoMember(6)]
        public int NanoPayExpiration { get; set; }
    }
}
