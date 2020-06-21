using Chaos.NaCl;

using NknSdk.Common.Extensions;

namespace NknSdk.Common
{
    public class CryptoKeyInfo
    {
        public string PublicKey { get; set; }

        public byte[] PrivateKey { get; set; }

        public byte[] CurvePrivateKey { get; set; }

        public static CryptoKeyInfo FromSeed(byte[] seed)
        {
            var publicKey = new byte[Ed25519.PublicKeySizeInBytes];
            var privateKey = new byte[Ed25519.ExpandedPrivateKeySizeInBytes];

            Ed25519.KeyPairFromSeed(out publicKey, out privateKey, seed);

            return new CryptoKeyInfo
            {
                PrivateKey = privateKey,
                PublicKey = publicKey.ToHexString()
            };
        }
    }
}
