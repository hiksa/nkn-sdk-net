using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class SignatureChainTransaction : ISerializable
    {
        [ProtoMember(1)]
        public byte[] SignatureChain { get; set; }

        [ProtoMember(2)]
        public byte[] Submitter { get; set; }
    }
}
