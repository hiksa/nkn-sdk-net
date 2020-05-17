using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class IssueAsset
    {
        [ProtoMember(1)]
        public byte[] Sender { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public string Symbol { get; set; }

        [ProtoMember(4)]
        public long TotalSupply { get; set; }

        [ProtoMember(5)]
        public int Precision { get; set; }
    }
}
