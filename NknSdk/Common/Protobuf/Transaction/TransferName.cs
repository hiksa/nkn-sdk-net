using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class TransferName : ISerializable
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Registrant { get; set; }

        [ProtoMember(3, IsPacked = true)]
        public byte[] Recipient { get; set; }
    }
}
