using System.Collections.Generic;

using NknSdk.Wallet.Models;

namespace NknSdk.Common.Options
{
    public class WalletOptions
    {
        public WalletOptions()
        {
            this.RpcServerAddress = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet";
            this.Password = "";
            this.Attributes = "";
            this.Version = NknSdk.Wallet.Wallet.DefaultVersion;

            this.Offset = 0;
            this.Limit = 1000;
            this.Meta = false;

            this.PasswordKeys = new Dictionary<int, string>();
        }

        public string SeedHex { get; set; }

        public string Password { get; set; }

        public string RpcServerAddress { get; set; }

        public string MasterKey { get; set; }

        public string Iv { get; set; }

        public bool TxPool { get; set; }

        public int? Version { get; set; }

        public string PasswordKey { get; set; }

        public long? Nonce { get; set; }

        public long? Fee { get; set; }

        public string Attributes { get; set; }

        public int Offset { get; set; }

        public int Limit { get; set; }

        public bool Meta { get; set; }

        public IDictionary<int, string> PasswordKeys { get; set; }

        public ScryptParams Scrypt { get; set; }

        public static WalletOptions FromWalletJson(WalletJson wallet)
        {
            return new WalletOptions
            {
                Iv = wallet.Iv,
                MasterKey = wallet.MasterKey,
                Version = wallet.Version,
                Scrypt = wallet.Scrypt,
            };
        }

        public WalletOptions AssignFrom(WalletJson wallet)
        {
            this.Iv = wallet.Iv;
            this.MasterKey = wallet.MasterKey;
            this.Version = wallet.Version;
            this.Scrypt = wallet.Scrypt;

            return this;
        }

        public static WalletOptions NewFrom(ClientOptions clientOptions)
        {
            var walletOptions = new WalletOptions();

            return walletOptions.AssignFrom(clientOptions);
        }

        public static WalletOptions NewFrom(PublishOptions publishOptions)
        {
            var walletOptions = new WalletOptions();

            return walletOptions.AssignFrom(publishOptions);
        }

        public static WalletOptions NewFrom(TransactionOptions transactionOptions)
        {
            var walletOptions = new WalletOptions();

            return walletOptions.AssignFrom(transactionOptions);
        }

        public WalletOptions AssignFrom(TransactionOptions transactionOptions)
        {
            this.Fee = transactionOptions.Fee;
            this.Attributes = transactionOptions.Attributes;
            this.Nonce = transactionOptions.Nonce;

            return this;
        }

        public WalletOptions AssignFrom(ClientOptions clientOptions)
        {
            this.SeedHex = string.IsNullOrEmpty(clientOptions.SeedHex) ? this.SeedHex : clientOptions.SeedHex;
            this.RpcServerAddress = string.IsNullOrEmpty(clientOptions.RpcServerAddress) ? this.RpcServerAddress : clientOptions.RpcServerAddress;

            return this;
        }

        public WalletOptions AssignFrom(PublishOptions publishOptions)
        {
            this.Offset = publishOptions.Offset;
            this.Limit = publishOptions.Limit;
            this.Meta = publishOptions.Meta;
            this.TxPool = publishOptions.TxPool;

            return this;
        }

        public WalletOptions MergeWith(ClientOptions clientOptions)
        {
            var clone = this.Clone();

            clone.SeedHex = string.IsNullOrEmpty(clientOptions.SeedHex) ? this.SeedHex : clientOptions.SeedHex;
            clone.RpcServerAddress = string.IsNullOrEmpty(clientOptions.RpcServerAddress) ? this.RpcServerAddress : clientOptions.RpcServerAddress;

            return clone;
        }

        public WalletOptions MergeWith(PublishOptions publishOptions)
        {
            var clone = this.Clone();

            clone.Offset = publishOptions.Offset;
            clone.Limit = publishOptions.Limit;
            clone.Meta = publishOptions.Meta;
            clone.TxPool = publishOptions.TxPool;

            return clone;
        }

        public WalletOptions Clone()
        {
            return new WalletOptions
            {
                Attributes = this.Attributes,
                Fee = this.Fee,
                Iv = this.Iv,
                Limit = this.Limit,
                MasterKey = this.MasterKey,
                Meta = this.Meta,
                Nonce = this.Nonce,
                Offset = this.Offset,
                Password = this.Password,
                PasswordKey = this.PasswordKey,
                PasswordKeys = this.PasswordKeys,
                RpcServerAddress = this.RpcServerAddress,
                Scrypt = this.Scrypt,
                SeedHex = this.SeedHex,
                TxPool = this.TxPool,
                Version = this.Version
            };
        }
    }
}
