﻿namespace NknSdk.Wallet
{
    public class WalletJson
    {
        public int Version { get; set; }

        public string PasswordHash { get; set; }

        public string MasterKey { get; set; }

        public string Iv { get; set; }

        public string SeedEncrypted { get; set; }

        public string Address { get; set; }

        public string ProgramHash { get; set; }

        public string ContractData { get; set; }
    }
}