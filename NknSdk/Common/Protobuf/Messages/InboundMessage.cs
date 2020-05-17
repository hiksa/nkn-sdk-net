using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class InboundMessage : ISerializable
    {
        [ProtoMember(1)]
        public string Source { get; set; }

        [ProtoMember(2)]
        public byte[] Payload { get; set; }

        [ProtoMember(3)]
        public byte[] PreviousSignature { get; set; }
    }
}
