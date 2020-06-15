using NknSdk.Common.Exceptions;
using Utf8Json;

namespace NknSdk.Wallet.Models
{
    public class WalletJson
    {
        public int? Version { get; set; }

        public string MasterKey { get; set; }

        public string Iv { get; set; }

        public string SeedEncrypted { get; set; }

        public string Address { get; set; }

        public ScryptParams Scrypt { get; set; }

        public static WalletJson FromJson(string json)
        {
            var wallet = JsonSerializer.Deserialize<WalletJson>(json);

            if (wallet.Version == null
                || wallet.Version < Wallet.MinCompatibleVersion
                || wallet.Version > Wallet.MaxCompatibleVersion)
            {
                throw new InvalidWalletVersionException(
                    $"Invalid wallet version {wallet.Version}. " +
                    $"Should be between {Wallet.MinCompatibleVersion} and {Wallet.MaxCompatibleVersion}");
            }

            if (string.IsNullOrWhiteSpace(wallet.MasterKey))
            {
                throw new InvalidWalletFormatException("Missing MasterKey property");
            }

            if (string.IsNullOrWhiteSpace(wallet.Iv))
            {
                throw new InvalidWalletFormatException("Missing Iv property");
            }

            if (string.IsNullOrWhiteSpace(wallet.SeedEncrypted))
            {
                throw new InvalidWalletFormatException("Missing SeedEncrypted property");
            }

            if (string.IsNullOrWhiteSpace(wallet.Address))
            {
                throw new InvalidWalletFormatException("Missing Address property");
            }

            return wallet;
        }
    }
}
