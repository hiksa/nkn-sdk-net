using System.Text.RegularExpressions;

namespace NknSdk.Common
{
    public class Constants
    {
        public class RpcResponseCodes
        {
            public const int Success = 0;

            public const int WrongNode = 48001;

            public const int AppendTxnPool = 45021;
        }

        public class MessageActions
        {
            public const string SetClient = "setClient";

            public const string UpdateSigChainBlockHash = "updateSigChainBlockHash";
        }

        public const string RpcServerAddress = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet";

        public const int MessageIdLength = 8;

        public const int SessionIdLength = 8;

        public const int MaxClientMessageSize = 4_000_000;

        public const int AcceptSessionBufferSize = 128;

        public const int CheckTimeoutInterval = 250;

        public static Regex DefaultSessionAllowedAddressRegex = new Regex("/.*/");

        public static Regex MultiClientIdentifierRegex = new Regex(@"^__\d+__$");
    }
}
