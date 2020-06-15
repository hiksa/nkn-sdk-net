using NknSdk.Common;

namespace NknSdk.Wallet.Models
{
    public class Account
    {
        private readonly CryptoKey key;

        public Account(string seedHex)
        {
            this.key = new CryptoKey(seedHex);

            this.SignatureRedeem = Common.Address.PublicKeyToSignatureRedeem(this.key.PublicKey);
            this.ProgramHash = Common.Address.HexStringToProgramHash(this.SignatureRedeem);
            this.Address = Common.Address.FromProgramHash(this.ProgramHash);
            this.Contract = Account.GenerateAccountContractString(this.SignatureRedeem, this.ProgramHash);
        }

        public string SignatureRedeem { get; }

        public string Address { get; }

        public string ProgramHash { get; }

        public string Contract { get; }

        public string PublicKey => this.key.PublicKey;

        public string Seed => this.key.SeedHex;

        public byte[] Sign(byte[] message) => this.key.Sign(message);

        private static string GenerateAccountContractString(string signatureRedeem, string programHash)
        {
            var contract = Common.Address.PrefixByteCountToHexString(signatureRedeem);

            contract += Common.Address.PrefixByteCountToHexString("00");
            contract += programHash;

            return contract;
        }
    }
}
