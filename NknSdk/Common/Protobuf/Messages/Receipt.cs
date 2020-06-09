using ProtoBuf;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class Receipt : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] PreviousSignature { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Signature { get; set; }
    }
}
