using NknSdk.Wallet;
using NknSdk.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NknSdk.Tests.Wallet
{
    public class AesTests
    {

        [Fact]
        public void ShouldEncryptCorrectly()
        {
            var plainText = "77ee9ec47b1b3f7c7a84f9c3039f6c04f0ecb42185c88cb27fe5aa1824ff72d6";
            var password = "5df6e0e2761359d30a8275058e299fcc0381534545f55cf43e41983f5d4c9456";
            var iv = "adfefb7853fa3316e30c418eded8a01f";

            var encrypted = Aes.Encrypt(plainText, password, iv.FromHexString());

            var expected = "e2c8be88a27bd350deb9214dd7a21b02344c2cb8ab6325820b4faeab4a603083";

            Assert.Equal(expected, encrypted);
        }

        [Fact]
        public void ShouldDecryptCorrectly()
        {

        }
    }
}
