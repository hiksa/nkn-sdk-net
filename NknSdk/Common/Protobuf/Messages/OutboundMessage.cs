using ProtoBuf;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class OutboundMessage : ISerializable
    {
        [ProtoMember(1)]
        public string Destination { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Payload { get; set; }

        [ProtoMember(3)]
        public string[] Destinations { get; set; }

        [ProtoMember(4)]
        public uint MaxHoldingSeconds { get; set; }

        [ProtoMember(5)]
        public uint Nonce { get; set; }

        [ProtoMember(6, IsPacked = true)]
        public byte[] BlockHash { get; set; }

        [ProtoMember(7)]
        public byte[][] Signatures { get; set; }

        [ProtoMember(8)]
        public byte[][] Payloads { get; set; }
    }
}
