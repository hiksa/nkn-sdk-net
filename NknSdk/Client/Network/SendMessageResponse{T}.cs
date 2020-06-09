namespace NknSdk.Client.Network
{
    public class SendMessageResponse<T>
    {
        public byte[] MessageId { get; set; }

        public T Result { get; set; }

        public string Error { get; set; }
    }
}
