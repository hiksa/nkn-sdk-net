using NknSdk.Common.Protobuf.Messages;
using ProtoBuf;

namespace NknSdk.Common.Protobuf.SignatureChain
{
    [ProtoContract]
    public class SignatureChainElement : ISerializable
    {
        [ProtoMember(1)]
        public byte[] Id { get; set; }

        [ProtoMember(2)]
        public byte[] NextPublicKey { get; set; }

        [ProtoMember(3)]
        public bool IsMining { get; set; }

        [ProtoMember(4)]
        public byte[] Signature { get; set; }

        [ProtoMember(5)]
        public SignatureAlgorithm Algorithm { get; set; }

        [ProtoMember(6)]
        public byte[] Vrf { get; set; }

        [ProtoMember(7)]
        public byte[] Proof { get; set; }
    }
}
