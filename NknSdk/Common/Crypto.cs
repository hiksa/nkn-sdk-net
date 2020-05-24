using System.Security.Cryptography;
using System.Text;

using Chaos.NaCl;
using NSec.Cryptography;

namespace NknSdk.Common
{
    public class Crypto
    {
        public const int KeyLength = 32;
        public const int NonceLength = 24;
        public const int PublicKeyLength = 32;
        public const int SeedLength = 32;
        public const int SignatureLength = 64;

        public static SignatureAlgorithm Algorithm { get; } = SignatureAlgorithm.Ed25519;

        public static CryptoKeyPair MakeKeyPair(byte[] privateKeySeed)
        {
            var publicKey = new byte[Chaos.NaCl.Ed25519.PublicKeySizeInBytes];
            var privateKey = new byte[Chaos.NaCl.Ed25519.ExpandedPrivateKeySizeInBytes];

            Chaos.NaCl.Ed25519.KeyPairFromSeed(publicKey, privateKey, privateKeySeed);
            var key = Key.Import(Algorithm, privateKeySeed, KeyBlobFormat.RawPrivateKey);

            return new CryptoKeyPair
            {
                PrivateKey = privateKey,
                PublicKey = publicKey.ToHexString(),
                RealKey = key
            };
        }

        public static byte[] EncryptSymmetric(byte[] message, byte[] nonce, byte[] sharedKey)
            => XSalsa20Poly1305.Encrypt(message, sharedKey, nonce);
        
        public static byte[] DecryptSymmetric(byte[] message, byte[] nonce, byte[] sharedKey)
        {
            var decrpted = Sodium.SecretBox.Open(message, nonce, sharedKey);
          //  var test2 = XSalsa20Poly1305.TryDecrypt(message, sharedKey, nonce);
            return decrpted;
        }

        public static string Sha256(byte[] input)
        {
            using (var sha256 = SHA256Managed.Create())
            {
                var hash = sha256.ComputeHash(input);

                var hashString = hash.ToHexString();

                return hashString;
            }
        }

        public static string Sha256(string input)
        {
            var inputBytes = Encoding.Default.GetBytes(input);
            return Sha256(inputBytes);
        }

        public static string Sha256Hex(string input)
        {
            var inputBytes = input.FromHexString();
            return Sha256(inputBytes);
        }

        public static string DoubleSha256(byte[] input)
        {
            using (var sha256 = SHA256Managed.Create())
            {
                var hash = sha256.ComputeHash(input);
                var doubleHash = sha256.ComputeHash(hash);

                return doubleHash.ToHexString();
            }
        }

        public static string DoubleSha256(string input)
        {
            var inputBytes = Encoding.Default.GetBytes(input);
            return DoubleSha256(inputBytes);
        }
    }
}
