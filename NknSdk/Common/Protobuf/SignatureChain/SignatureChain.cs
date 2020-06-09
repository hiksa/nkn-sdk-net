using System.Collections.Generic;

using ProtoBuf;

namespace NknSdk.Common.Protobuf.SignatureChain
{
    [ProtoContract]
    public class SignatureChain : ISerializable
    {
        [ProtoMember(1)]
        public uint Nonce { get; set; }

        [ProtoMember(2)]
        public uint DataSize { get; set; }

        [ProtoMember(3, IsPacked = true)]
        public byte[] BlockHash { get; set; }

        [ProtoMember(4, IsPacked = true)]
        public byte[] SourceId { get; set; }

        [ProtoMember(5, IsPacked = true)]
        public byte[] SourcePublicKey { get; set; }

        [ProtoMember(6, IsPacked = true)]
        public byte[] DestinationId { get; set; }

        [ProtoMember(7, IsPacked = true)]
        public byte[] DestinationPublicKey { get; set; }

        [ProtoMember(8, IsPacked = true)]
        public IList<SignatureChainElement> Elements { get; set; }
    }
}
