using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Payload : ISerializable
    {
        [ProtoMember(1)]
        public PayloadType Type { get; set; }

        [ProtoMember(2)]
        public byte[] Data { get; set; }
    }
}
