using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

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

        [ProtoMember(4)]
        public byte[] Attributes { get; set; }
    }
}
