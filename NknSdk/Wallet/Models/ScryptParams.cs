namespace NknSdk.Wallet.Models
{
    public class ScryptParams
    {
        public ScryptParams()
        {
            this.SaltLength = 8;
            this.N = 1 << 15;
            this.R = 8;
            this.P = 1;
        }

        public int SaltLength { get; set; }

        public int N { get; set; }

        public int R { get; set; }

        public int P { get; set; }

        public string Salt { get; set; }
    }
}
