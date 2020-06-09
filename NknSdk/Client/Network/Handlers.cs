using NknSdk.Client.Network;

namespace NknSdk.Client.Network
{
    public class Handlers
    {
        public delegate void ConnectHandler(ConnectHandlerRequest request);
        public delegate void TextMessageHandler(string sender, string text);
        public delegate void DataMessageHandler(string sender, byte[] data);
        public delegate void SessionMessageHandler(string sender, byte[] data);
        public delegate void ResponseHandler(object data);
        public delegate void TimeoutHandler(object error);
    }
}
