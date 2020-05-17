namespace NknSdk.Wallet
{
    public class WalletOptions
    {
        public string SeedHex { get; set; }

        public string Password { get; set; }

        public string RpcServer { get; set; }

        public byte[] MasterKey { get; set; }

        public byte[] Iv { get; set; }

        public bool TxPool { get; set; }

        public static WalletOptions Default { get; } = new WalletOptions
        {
            RpcServer = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet"
        };
    }
}
