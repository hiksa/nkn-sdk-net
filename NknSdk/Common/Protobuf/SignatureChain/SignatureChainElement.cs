using ProtoBuf;

namespace NknSdk.Common.Protobuf.SignatureChain
{
    [ProtoContract]
    public class SignatureChainElement : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Id { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] NextPublicKey { get; set; }

        [ProtoMember(3)]
        public bool IsMining { get; set; }

        [ProtoMember(4, IsPacked = true)]
        public byte[] Signature { get; set; }

        [ProtoMember(5)]
        public SignatureAlgorithm Algorithm { get; set; }

        [ProtoMember(6, IsPacked = true)]
        public byte[] Vrf { get; set; }

        [ProtoMember(7, IsPacked = true)]
        public byte[] Proof { get; set; }
    }
}
