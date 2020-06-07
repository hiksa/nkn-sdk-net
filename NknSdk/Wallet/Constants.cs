namespace NknSdk.Wallet
{
    public class Constants
    {
        public const int NameRegistrationFee = 10;
        public const int Version = 1;
        public const int MinCompatibleVersion = 1;
        public const int MaxCompatibleVersion = 2;

        public const int Uint160Length = 20;
        public const int CheckSumLength = 4;

        public const string AddressPrefix = "02b825";
        public static readonly int AddressPrefixLength = AddressPrefix.Length / 2;
        public static readonly int AddressLength = AddressPrefixLength + Uint160Length + CheckSumLength;
        public static readonly ScryptParams DefaultScryptParams = new ScryptParams
        {
            SaltLength = 8,
            N = 1 << 15,
            R = 8,
            P = 1
        };
    }
}
