using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class SignatureChainTransaction : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] SignatureChain { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Submitter { get; set; }
    }
}
