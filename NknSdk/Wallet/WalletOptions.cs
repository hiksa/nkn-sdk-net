using NknSdk.Common;
using NknSdk.Common.Exceptions;
using System.Collections;
using System.Collections.Generic;

namespace NknSdk.Wallet
{
    public class WalletOptions
    {
        public WalletOptions()
        {
            this.RpcServer = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet";
            this.Password = "";

            this.PasswordKeys = new Dictionary<int, string>();
        }

        public string SeedHex { get; set; }

        public string Password { get; set; }

        public string RpcServer { get; set; }

        public byte[] MasterKey { get; set; }

        public string Iv { get; set; }

        public bool TxPool { get; set; }

        public int? Version { get; set; }

        public string PasswordKey { get; set; }

        public ulong? Nonce { get; set; }

        public long? Fee { get; set; }

        public bool BuildOnly { get; set; }

        public string Attributes { get; set; }

        public IDictionary<int, string> PasswordKeys { get; set; }

        public ScryptParams Scrypt { get; set; }

        public static WalletOptions Default { get; } = new WalletOptions
        {
            RpcServer = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet"
        };

        public static WalletOptions FromWalletJson(WalletJson wallet)
        {
            return new WalletOptions
            {
                Iv = wallet.Iv,
                MasterKey = wallet.MasterKey.FromHexString(),
                Version = wallet.Version,
                Scrypt = wallet.Scrypt,
            };
        }
    }
}
