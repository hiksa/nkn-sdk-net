using System;

using Xunit;

using NknSdk.Wallet;
using NknSdk.Common.Extensions;
using System.Collections.Generic;

namespace NknSdk.Tests.Wallet
{
    public class AesTests
    {
        [Fact]
        public void ShouldEncryptCorrectly()
        {
            var input = new List<(string plainText, string password, string iv, string expected)>
            {
                (
                    "77ee9ec47b1b3f7c7a84f9c3039f6c04f0ecb42185c88cb27fe5aa1824ff72d6", 
                    "5df6e0e2761359d30a8275058e299fcc0381534545f55cf43e41983f5d4c9456",
                    "adfefb7853fa3316e30c418eded8a01f",
                    "e2c8be88a27bd350deb9214dd7a21b02344c2cb8ab6325820b4faeab4a603083"
                ),
                (
                    "f40bc0903ae5064ec225a82d1ca8ca6c63d4ecf2c41689b82fc6bf581ea3b67b",
                    "2b593d352785eee69106325f56014ddf8724ab6403ca30d7bb62a964b41b688d",
                    "e989169aa3f1d8ac191e211549fd5de6",
                    "9a406f5c6b3212cc43af7ba2cda841afcf29f2643cabf8b76247b912f7dbe73f"
                )
            };

            foreach (var (plainText, password, iv, expected) in input)
            {
                var encrypted = Aes.Encrypt(plainText, password, iv.FromHexString());
                Assert.Equal(expected, encrypted);
            }
        }

        [Fact]
        public void ShouldDecryptCorrectly()
        {
            var input = new List<(string cipherText, string password, string iv, string expected)>
            {
                (
                    "689066ba1b5fe6cd89f7836b3bec608c4b2c5491ec637a05ffd8913a62082708",
                    "15c6eb523e497a59925b9a15a8b62cf0f7f95538524c87856f9ed2df5c79f92f",
                    "af937f5dee6595dd1fe3c75454169294",
                    "b4d5ac923dcf82aa2174a5cea4f2ab732f73944f875222ea2d1c55f5e6fb9219"
                ),
                (
                    "9a406f5c6b3212cc43af7ba2cda841afcf29f2643cabf8b76247b912f7dbe73f",
                    "2b593d352785eee69106325f56014ddf8724ab6403ca30d7bb62a964b41b688d",
                    "e989169aa3f1d8ac191e211549fd5de6",
                    "f40bc0903ae5064ec225a82d1ca8ca6c63d4ecf2c41689b82fc6bf581ea3b67b"
                )
            };

            foreach (var (cipherText, password, iv, expected) in input)
            {
                var decrypted = Aes.Decrypt(cipherText, password, iv.FromHexString());
                Assert.Equal(expected, decrypted);
            }
        }
    }
}
