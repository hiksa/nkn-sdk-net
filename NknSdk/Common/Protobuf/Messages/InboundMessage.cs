using ProtoBuf;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class InboundMessage : ISerializable
    {
        [ProtoMember(1)]
        public string Source { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Payload { get; set; }

        [ProtoMember(3, IsPacked = true)]
        public byte[] PreviousSignature { get; set; }
    }
}
