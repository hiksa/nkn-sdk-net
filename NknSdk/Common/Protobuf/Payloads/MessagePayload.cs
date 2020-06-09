using ProtoBuf;

namespace NknSdk.Common.Protobuf.Payloads
{
    [ProtoContract]
    public class MessagePayload : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Payload { get; set; }

        [ProtoMember(2)]
        public bool IsEncrypted { get; set; }

        [ProtoMember(3, IsPacked = true)]
        public byte[] Nonce { get; set; }

        [ProtoMember(4)]
        public byte[] EncryptedKey { get; set; }
    }
}
