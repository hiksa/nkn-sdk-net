using NknSdk.Common.Exceptions;

namespace NknSdk.Common.Options
{
    public class TransactionOptions
    {
        public TransactionOptions()
        {
            this.RpcServerAddress = Constants.RpcServerAddress;
            this.Attributes = "";
        }

        public string RpcServerAddress { get; set; }

        public long? Fee { get; set; }

        public string Attributes { get; set; }

        public long? Nonce { get; set; }

        public static TransactionOptions NewFrom(ClientOptions clientOptions)
        {
            var options = new TransactionOptions { RpcServerAddress = clientOptions.RpcServerAddress };

            return options;
        }

        public static TransactionOptions NewFrom(WalletOptions walletOptions)
        {
            var options = new TransactionOptions();

            return options.AssignFrom(walletOptions);
        }

        public TransactionOptions AssignFrom(WalletOptions walletOptions)
        {
            this.Attributes = walletOptions.Attributes;
            this.Fee = walletOptions.Fee;
            this.Nonce = walletOptions.Nonce;
            this.RpcServerAddress = walletOptions.RpcServerAddress;

            return this;
        }
    }
}
