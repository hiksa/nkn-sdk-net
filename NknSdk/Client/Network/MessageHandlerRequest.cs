using NknSdk.Common.Protobuf.Payloads;

namespace NknSdk.Client.Network
{
    public class MessageHandlerRequest
    {
        public string Source { get; set; }

        public byte[] Payload { get; set; }

        public PayloadType PayloadType { get; set; }

        public bool IsEncrypted { get; set; }

        public byte[] MessageId { get; set; }

        public bool NoReply { get; set; }
    }
}
