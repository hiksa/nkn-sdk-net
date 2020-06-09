using NSec.Cryptography;

using NknSdk.Common.Extensions;

namespace NknSdk.Common
{
    public class CryptoKeyInfo
    {
        public Key Key { get; set;  }

        public string PublicKey { get; set; }

        public byte[] PrivateKey { get; set; }

        public byte[] CurvePrivateKey { get; set; }

        public static CryptoKeyInfo FromSeed(byte[] seed)
        {
            var publicKey = new byte[Chaos.NaCl.Ed25519.PublicKeySizeInBytes];
            var privateKey = new byte[Chaos.NaCl.Ed25519.ExpandedPrivateKeySizeInBytes];

            Chaos.NaCl.Ed25519.KeyPairFromSeed(out publicKey, out privateKey, seed);

            var key = Key.Import(Hash.Algorithm, seed, KeyBlobFormat.RawPrivateKey);

            return new CryptoKeyInfo
            {
                PrivateKey = privateKey,
                PublicKey = publicKey.ToHexString(),
                Key = key
            };
        }
    }
}
