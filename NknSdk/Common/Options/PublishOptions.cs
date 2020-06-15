namespace NknSdk.Common.Options
{
    public class PublishOptions
    {
        public PublishOptions()
        {
            this.NoReply = true;

            this.TxPool = false;
            this.Meta = false;
            this.Offset = 0;
            this.Limit = 1000;
        }

        public bool TxPool { get; set; }

        public bool Meta { get; set; }

        public int Offset { get; set; }

        public int Limit { get; set; }

        public bool NoReply { get; set; }

        public PublishOptions Clone()
        {
            return new PublishOptions
            {
                NoReply = this.NoReply,
                TxPool = this.TxPool,
                Meta = this.Meta,
                Offset = this.Offset,
                Limit = this.Limit
            };
        }
    }
}
