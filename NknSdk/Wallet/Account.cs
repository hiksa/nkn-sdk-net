using NknSdk.Common;
using System;
using System.Collections.Generic;
using System.Text;
using static NknSdk.Wallet.Address;

namespace NknSdk.Wallet
{
    public class Account
    {
        private readonly CryptoKey key;

        public Account(string seedHex)
        {
            this.key = new CryptoKey(seedHex);

            this.SignatureRedeem = PublicKeyToSignatureRedeem(this.key.PublicKey);
            this.ProgramHash = HexStringToProgramHash(this.SignatureRedeem);
            this.Address = ProgramHashStringToAddress(this.ProgramHash);
            this.Contract = GenerageAccountContractString(this.SignatureRedeem, this.ProgramHash);
        }

        public string SignatureRedeem { get; }

        public string Address { get; }

        public string ProgramHash { get; }

        public string Contract { get; }

        public string PublicKey => this.key.PublicKey;

        public string Seed => this.key.Seed;

        public byte[] Sign(byte[] message) => this.key.Sign(message);

        private string GenerageAccountContractString(string signatureRedeem, string programHash)
        {
            var contract = PrefixByteCountToHexString(signatureRedeem);
            contract += PrefixByteCountToHexString("00");
            contract += programHash;

            return contract;
        }
    }
}
