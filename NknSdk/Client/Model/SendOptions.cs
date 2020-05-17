namespace NknSdk.Client.Model
{
    public class SendOptions
    {
        public int? ResponseTimeout { get; set; }

        public bool? IsEncrypted { get; set; }

        public int? HoldingSeconds { get; set; }

        public bool? NoReply { get; set; }

        public string MessageId { get; set; }

        public string ReplyToId { get; set; }
    }
}
