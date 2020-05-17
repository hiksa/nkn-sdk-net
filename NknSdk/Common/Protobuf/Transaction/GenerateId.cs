using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class GenerateId
    {
        [ProtoMember(1)]
        public byte[] PublicKey { get; set; }

        [ProtoMember(2)]
        public long RegistrationFee { get; set; }
    }
}
