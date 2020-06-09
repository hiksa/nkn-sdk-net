using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Unsubscribe : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Subscriber { get; set; }

        [ProtoMember(2)]
        public string Identifier { get; set; }

        [ProtoMember(3)]
        public string Topic { get; set; }
    }
}
