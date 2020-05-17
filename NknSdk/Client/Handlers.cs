using NknSdk.Client.Model;

namespace NknSdk.Client
{
    public class Handlers
    {
        public delegate void ConnectHandler(ConnectRequest request);
        public delegate void TextMessageHandler(string sender, string text);
        public delegate void DataMessageHandler(string sender, byte[] data);
        public delegate void SessionMessageHandler(string sender, byte[] data);
        public delegate void ResponseHandler(object data);
        public delegate void TimeoutHandler(object error);
    }
}
