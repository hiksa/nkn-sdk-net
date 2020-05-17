using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class DeleteName
    {
        [ProtoMember(1)]
        public byte[] Registrant { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }
    }
}
