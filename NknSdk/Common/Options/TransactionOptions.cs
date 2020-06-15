namespace NknSdk.Common.Options
{
    public class TransactionOptions
    {
        public TransactionOptions()
        {
            this.Attributes = "";
        }

        public long? Fee { get; set; }

        public string Attributes { get; set; }

        public long? Nonce { get; set; }
    }
}
