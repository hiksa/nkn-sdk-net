using Xunit;

using NknSdk.Common;
using NknSdk.Common.Extensions;

namespace NknSdk.Tests.Common
{
    public class HashingTests
    {
        [Fact]
        public void ShouldDecryptSymmetricCorrectly()
        {
            var expectedMessageBytes = new byte[] { 45, 219, 127, 108, 13, 125, 208, 244, 202, 174, 100, 234, 87, 122, 145, 255, 189, 118, 109, 224, 170, 132, 20, 106, 104, 110, 232, 15, 16, 157, 166, 19 };
            var expectedNonce = new byte[] { 55, 156, 23, 18, 59, 154, 252, 109, 114, 164, 45, 28, 73, 64, 13, 36, 111, 8, 20, 76, 22, 61, 89, 85 };
            var expectedSourcePublicKey = "3fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6e";
            var expectedSharedKey = "ffc4e152771ea860a528012c25c5f6bcc24bcb758d9d5cf8928ac1cb1449f1ff";
            
            var expectedResult = new byte[] { 18, 8, 120, 202, 34, 185, 226, 92, 173, 197, 26, 4, 1, 2, 3, 5 };
            
            var seed = "f40bc0903ae5064ec225a82d1ca8ca6c63d4ecf2c41689b82fc6bf581ea3b67b";
            var key = new CryptoKey(seed);
            var sharedKey = key.GetSharedSecret(expectedSourcePublicKey);
            
            Assert.Equal(expectedSharedKey, sharedKey.ToHexString());

            var result = Hash.DecryptSymmetric(expectedMessageBytes, expectedNonce, sharedKey);
            
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Sha256HexHashString_Should_CalculateCorrectly()
        {
            var expectedHash = "df7e70e5021544f4834bbee64a9e3789febc4be81470df629cad6ddb03320a5c";

            var bytesString = "42";

            var hash = Hash.Sha256Hex(bytesString);

            Assert.Equal(expectedHash, hash);
        }

        [Fact]
        public void DoubleSha256HexHashString_Should_CalculateCorrectly()
        {
            var expectedHash = "01ce41ef545f9bf830a3b3f161d424e4e4f3c9ebb10eb5a305af9e327f3abb43";

            var bytesString = "42";

            var hash = Hash.DoubleSha256(bytesString);

            Assert.Equal(expectedHash, hash);
        }

        [Fact]
        public void Sha256Hex_Should_CalculateCorrectly()
        {
            var password = "123";
            var passwordHash = Hash.DoubleSha256(password);
            var expectedPasswordHash = "5a77d1e9612d350b3734f6282259b7ff0a3f87d62cfef5f35e91a5604c0490a3";

            Assert.Equal(passwordHash, expectedPasswordHash);

            var sha256Hash = Hash.Sha256Hex(expectedPasswordHash);
            var expectedSha256Hash = "3180b4071170db0ae9f666167ed379f53468463f152e3c3cfb57d1de45fd01d6";

            Assert.Equal(expectedSha256Hash, sha256Hash);
        }
    }
}
