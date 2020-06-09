namespace NknSdk.Client.Network
{
    public class SendMessageResponse<TResult>
    {
        public byte[] MessageId { get; set; }

        public TResult Result { get; set; }

        public string Error { get; set; }
    }
}
