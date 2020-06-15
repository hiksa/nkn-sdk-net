using NknSdk.Common.Rpc.Results;

namespace NknSdk.Client.Messages
{
    public class WrongNodeMessage : Message
    {
        public GetWsAddressResult Result { get; set; }
    }
}
