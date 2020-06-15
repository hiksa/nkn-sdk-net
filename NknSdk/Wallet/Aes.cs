using System;
using System.IO;
using System.Security.Cryptography;

using NknSdk.Common.Extensions;

namespace NknSdk.Wallet
{
    public static class Aes
    {
        public static string Encrypt(string plainText, string keyHex, byte[] iv)
        {
            return EncryptStringToBytesAes(plainText, keyHex.FromHexString(), iv).ToHexString();
        }

        public static string Decrypt(string cipherTextHex, string keyHex, byte[] iv)
        {
            return DecryptStringFromBytesAes(cipherTextHex.FromHexString(), keyHex.FromHexString(), iv);
        }

        private static byte[] EncryptStringToBytesAes(string plainText, byte[] key, byte[] iv)
        {
            if (plainText == null || plainText.Length <= 0)
            {
                throw new ArgumentNullException("plainText");
            }

            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException("Key");
            }

            if (iv == null || iv.Length <= 0)
            {
                throw new ArgumentNullException("IV");
            }

            var data = plainText.FromHexString();

            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                aes.Mode = CipherMode.CBC;
                aes.Key = key;
                aes.IV = iv;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var msEncrypt = new MemoryStream())
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(data, 0, data.Length);

                    return msEncrypt.ToArray();
                }
            }
        }

        private static string DecryptStringFromBytesAes(byte[] cipherText, byte[] key, byte[] iv)
        {
            if (cipherText == null || cipherText.Length <= 0)
            {
                throw new ArgumentNullException("cipherText");
            }

            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException("Key");
            }

            if (iv == null || iv.Length <= 0)
            {
                throw new ArgumentNullException("IV");
            }

            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                aes.Mode = CipherMode.CBC;
                aes.Key = key;
                aes.IV = iv;

                using (var output = new MemoryStream())                
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipherText))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    cs.CopyTo(output);

                    return output.ToArray().ToHexString();
                }
            }
        }
    }
}
