using NknSdk.Common.Protobuf.Messages;
using ProtoBuf;

namespace NknSdk.Common.Protobuf.Payloads
{
    [ProtoContract]
    public class TextDataPayload : ISerializable
    {
        [ProtoMember(1)]
        public string Text { get; set; }
    }
}
