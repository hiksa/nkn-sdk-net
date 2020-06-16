namespace NknSdk.Common.Options
{
    public class SendOptions
    {
        public SendOptions()
        {
            this.IsEncrypted = true;
            this.MessageId = PseudoRandom.RandomBytesAsHexString(Constants.MessageIdLength);
            this.ResponseTimeout = 5_000;
        }

        public int? ResponseTimeout { get; set; }

        public bool? IsEncrypted { get; set; }

        public int? HoldingSeconds { get; set; }

        public bool? NoReply { get; set; }

        public string MessageId { get; set; }

        public string ReplyToId { get; set; }

        public static SendOptions NewFrom(PublishOptions publishOptions)
        {
            var options = new SendOptions();

            return options.AssignFrom(publishOptions);
        }

        public SendOptions AssignFrom(PublishOptions publishOptions)
        {
            this.NoReply = publishOptions.NoReply;

            return this;
        }
    }
}
