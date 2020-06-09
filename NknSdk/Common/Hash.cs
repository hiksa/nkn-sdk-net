using System.Security.Cryptography;
using System.Text;

using Chaos.NaCl;
using HashLib;
using NSec.Cryptography;

using NknSdk.Common.Extensions;

namespace NknSdk.Common
{
    public class Hash
    {
        public const int KeyLength = 32;
        public const int NonceLength = 24;
        public const int PublicKeyLength = 32;
        public const int SeedLength = 32;
        public const int SignatureLength = 64;

        public static SignatureAlgorithm Algorithm { get; } = SignatureAlgorithm.Ed25519;

        public static byte[] EncryptSymmetric(byte[] message, byte[] nonce, byte[] sharedKey)
        {
            return XSalsa20Poly1305.Encrypt(message, sharedKey, nonce);
        }
        
        public static byte[] DecryptSymmetric(byte[] message, byte[] nonce, byte[] sharedKey)
        {
            return Sodium.SecretBox.Open(message, nonce, sharedKey);
          //  var test2 = XSalsa20Poly1305.TryDecrypt(message, sharedKey, nonce);
        }

        public static string Sha256(byte[] input)
        {
            using (var sha256 = SHA256Managed.Create())
            {
                var hash = sha256.ComputeHash(input);

                return hash.ToHexString();
            }
        }

        public static string Sha256(string input)
        {
            var inputBytes = Encoding.Default.GetBytes(input);

            return Hash.Sha256(inputBytes);
        }

        public static string Sha256Hex(string hexString)
        {
            var inputBytes = hexString.FromHexString();

            return Hash.Sha256(inputBytes);
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

            return Hash.DoubleSha256(inputBytes);
        }

        public static string Ripemd160(string text)
        {
            var hash = HashFactory.Crypto.CreateRIPEMD160();

            var result = hash.ComputeString(text);

            return result.GetBytes().ToHexString();
        }

        public static byte[] Ripemd160Hex(string hex)
        {
            var bytes = hex.FromHexString();

            var hash = HashFactory.Crypto.CreateRIPEMD160();

            var result = hash.ComputeBytes(bytes);

            return result.GetBytes();
        }
    }
}
