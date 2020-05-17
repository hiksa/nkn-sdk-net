using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class OutboundMessage : ISerializable
    {
        [ProtoMember(1)]
        public string Destination { get; set; }

        [ProtoMember(2)]
        public byte[] Payload { get; set; }

        [ProtoMember(3)]
        public IList<string> Destinations { get; set; }

        [ProtoMember(4)]
        public uint MaxHoldingSeconds { get; set; }

        [ProtoMember(5)]
        public uint Nonce { get; set; }

        [ProtoMember(6)]
        public byte[] BlockHash { get; set; }

        [ProtoMember(7)]
        public IList<byte[]> Signatures { get; set; }

        [ProtoMember(8)]
        public IList<byte[]> Payloads { get; set; }
    }
}
