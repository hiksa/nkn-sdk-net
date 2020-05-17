using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common
{
    public class CryptoKeyPair
    {
        public NSec.Cryptography.Key RealKey { get; set;  }

        public string PublicKey { get; set; }

        public byte[] PrivateKey { get; set; }

        public byte[] CurvePrivateKey { get; set; }
    }
}
