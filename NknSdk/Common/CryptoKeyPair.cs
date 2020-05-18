using NSec.Cryptography;

namespace NknSdk.Common
{
    public class CryptoKeyPair
    {
        public Key RealKey { get; set;  }

        public string PublicKey { get; set; }

        public byte[] PrivateKey { get; set; }

        public byte[] CurvePrivateKey { get; set; }
    }
}
