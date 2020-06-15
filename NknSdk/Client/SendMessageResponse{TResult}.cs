namespace NknSdk.Client
{
    public class SendMessageResponse<TResult>
    {
        public byte[] MessageId { get; set; }

        public TResult Result { get; set; }

        public string ErrorMessage { get; set; }
    }
}
