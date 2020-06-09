using ProtoBuf;

namespace NknSdk.Common.Protobuf.Payloads
{
    [ProtoContract]
    public class Payload : ISerializable
    {
        [ProtoMember(1)]
        public PayloadType Type { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] MessageId { get; set; }

        [ProtoMember(3, IsPacked = true)]
        public byte[] Data { get; set; }

        [ProtoMember(4, IsPacked = true)]
        public byte[] ReplyToId { get; set; }

        [ProtoMember(5)]
        public bool NoReply { get; set; }
    }
}
