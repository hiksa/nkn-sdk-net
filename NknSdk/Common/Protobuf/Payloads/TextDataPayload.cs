using ProtoBuf;

namespace NknSdk.Common.Protobuf.Payloads
{
    [ProtoContract]
    public class TextDataPayload
    {
        [ProtoMember(1)]
        public string Text { get; set; }
    }
}
