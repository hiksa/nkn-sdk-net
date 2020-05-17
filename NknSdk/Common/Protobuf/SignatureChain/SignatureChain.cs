using System.Collections.Generic;
using ProtoBuf;

namespace NknSdk.Common.Protobuf.SignatureChain
{
    [ProtoContract]
    public class SignatureChain
    {
        [ProtoMember(1)]
        public uint Nonce { get; set; }

        [ProtoMember(2)]
        public uint DataSize { get; set; }

        [ProtoMember(3)]
        public byte[] BlockHash { get; set; }

        [ProtoMember(4)]
        public byte[] SourceId { get; set; }

        [ProtoMember(5)]
        public byte[] SourcePublicKey { get; set; }

        [ProtoMember(6)]
        public byte[] DestinationId { get; set; }

        [ProtoMember(7)]
        public byte[] DestinationPublicKey { get; set; }

        [ProtoMember(8)]
        public IList<SignatureChainElement> Elements { get; set; }
    }
}
