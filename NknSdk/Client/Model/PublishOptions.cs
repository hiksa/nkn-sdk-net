namespace NknSdk.Client.Model
{
    public class PublishOptions
    {
        public bool? IsEncrypted { get; set; }

        public int? MessageHoldingSeconds { get; set; }

        public byte[] MessageId { get; set; }

        public byte[] ReplyToId { get; set; }

        public bool? TxPool { get; set; }

        public int? Offset { get; set; }

        public int? Limit { get; set; }
    }
}
