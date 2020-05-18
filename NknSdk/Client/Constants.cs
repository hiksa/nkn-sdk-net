using System.Text.RegularExpressions;

namespace NknSdk.Client
{
    public class Constants
    {
        public const string RpcServerAddress = "http://mainnet-seed-0001.nkn.org:30003";

        public const int MessageIdLength = 8;

        public const int MaxClientMessageSize = 4_000_000;

        public const int AcceptSessionBufferSize = 128;

        public const int SessionIdSize = 8;

        public static Regex DefaultSessionAllowedAddressRegex = new Regex("/.*/");

        public static Regex MultiClientIdentifierRegex = new Regex(@"^__\d+__$");
    }
}
