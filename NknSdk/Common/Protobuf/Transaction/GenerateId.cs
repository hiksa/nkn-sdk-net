using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class GenerateId : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] PublicKey { get; set; }

        [ProtoMember(2)]
        public long RegistrationFee { get; set; }
    }
}
