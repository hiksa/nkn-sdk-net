using System.Linq;

using Xunit;

using NknSdk.Common;

namespace NknSdk.Tests.Common
{
    public class CryptoKeyTests
    {
        [Fact]
        public void ShouldGetCorrectSharedKey()
        {
            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);
            var otherPublicKey = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";

            var sharedSecret = key.GetSharedSecret(otherPublicKey);
            var expectedSharedSecret = new byte[] { 6, 64, 52, 108, 146, 127, 50, 182, 72, 214, 157, 195, 104, 69, 154, 41, 175, 17, 58, 20, 213, 139, 252, 129, 133, 168, 15, 154, 224, 136, 229, 104 };

            Assert.Equal(expectedSharedSecret, sharedSecret);
        }

        [Fact]
        public void Should_Decrypt_Correctly()
        {
            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);

            var rawPayload = new byte[] { 229, 71, 56, 152, 98, 140, 128, 240, 228, 123, 35, 251, 110, 123, 164, 159, 58, 191, 9, 120, 102, 190, 91, 18, 157, 26, 5, 84, 82, 239, 49, 148, 225, 239, 170, 23, 171, 225, 19, 118, 227, 149, 250, 145, 232, 29, 189, 208 };
            var sourcePublicKey = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";
            var nonce = new byte[] { 224, 178, 188, 14, 5, 10, 133, 111, 74, 130, 193, 62, 242, 48, 81, 162, 153, 95, 157, 100, 42, 43, 70, 117 };

            var expectedSharedKey = new byte[] { 6, 64, 52, 108, 146, 127, 50, 182, 72, 214, 157, 195, 104, 69, 154, 41, 175, 17, 58, 20, 213, 139, 252, 129, 133, 168, 15, 154, 224, 136, 229, 104 };
            var expectedDecryptedPayload = new byte[] { 89, 254, 248, 159, 83, 78, 29, 213, 117, 249, 44, 236, 188, 167, 153, 187, 119, 179, 151, 61, 251, 170, 182, 150, 103, 135, 210, 16, 214, 242, 78, 74 };

            var actualSharedKey = key.GetSharedSecret(sourcePublicKey);
            var actualDecryptedPayload = key.Decrypt(rawPayload, nonce, sourcePublicKey);

            Assert.Equal(expectedSharedKey, actualSharedKey);
            Assert.Equal(expectedDecryptedPayload, actualDecryptedPayload);
        }

        [Fact]
        public void Should_Decrypt_Correctly2()
        {
            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);

            var rawPayload = new byte[] { 9, 38, 43, 237, 238, 37, 215, 74, 80, 126, 210, 146, 118, 16, 25, 181, 47, 67, 152, 20, 60, 133, 92, 170, 205, 214, 252, 24, 177, 227, 252 };
            var sourcePublicKey = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";
            var nonce = new byte[] { 69, 226, 203, 123, 230, 12, 117, 45, 42, 151, 134, 56, 99, 218, 166, 216, 21, 207, 247, 229, 105, 205, 26, 174 };

            var expectedSharedKey = new byte[] { 6, 64, 52, 108, 146, 127, 50, 182, 72, 214, 157, 195, 104, 69, 154, 41, 175, 17, 58, 20, 213, 139, 252, 129, 133, 168, 15, 154, 224, 136, 229, 104 };
            var expectedDecryptedPayload = new byte[] { 18, 8, 63, 156, 178, 174, 15, 23, 199, 41, 26, 3, 1, 2, 3 };

            var actualSharedKey = key.GetSharedSecret(sourcePublicKey);
            var actualDecryptedPayload = key.Decrypt(rawPayload, nonce, sourcePublicKey);

            Assert.Equal(expectedSharedKey, actualSharedKey);
            Assert.Equal(expectedDecryptedPayload, actualDecryptedPayload);
        }

        [Fact]
        public void Should_Decrypt_WithNoEncryptedKey_FromJS()
        {
            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);

            var rawPayload = new byte[] { 46, 20, 25, 86, 151, 139, 194, 25, 251, 243, 183, 191, 119, 126, 88, 31, 115, 245, 168, 149, 38, 101, 56, 27, 205, 159, 147, 158, 85, 255, 104 };
            var sourcePublicKey = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";
            var nonce = new byte[] { 166, 169, 129, 120, 11, 177, 14, 227, 143, 101, 67, 179, 196, 4, 42, 31, 1, 33, 153, 63, 161, 1, 196, 206 };

            var expectedSharedKey = new byte[] { 6, 64, 52, 108, 146, 127, 50, 182, 72, 214, 157, 195, 104, 69, 154, 41, 175, 17, 58, 20, 213, 139, 252, 129, 133, 168, 15, 154, 224, 136, 229, 104 };
            var expectedDecryptedPayload = new byte[] { 18, 8, 63, 57, 213, 197, 43, 137, 154, 199, 26, 3, 1, 2, 3 };

            var actualSharedKey = key.GetSharedSecret(sourcePublicKey);
            var actualDecryptedPayload = key.Decrypt(rawPayload, nonce, sourcePublicKey);

            Assert.Equal(expectedSharedKey, actualSharedKey);
            Assert.Equal(expectedDecryptedPayload, actualDecryptedPayload);
        }

        [Fact]
        public void Should_Decrypt_WithEncryptedKey_FromJS()
        {
            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);

            var rawPayload = new byte[] { 41, 133, 180, 228, 61, 56, 51, 196, 244, 191, 166, 88, 255, 54, 255, 183, 63, 179, 110, 211, 97, 105, 200, 181, 28, 65, 21, 27, 159, 185, 150 };
            var sourcePublicKey = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";
            var nonce = new byte[] { 99, 214, 219, 147, 69, 232, 24, 77, 181, 43, 32, 172, 46, 46, 83, 174, 211, 224, 54, 182, 71, 9, 190, 203, 129, 154, 158, 13, 243, 226, 210, 217, 222, 192, 92, 160, 24, 122, 0, 211, 76, 252, 29, 142, 217, 230, 176, 195 };
            var encryptedKey = new byte[] { 171, 196, 214, 242, 84, 245, 90, 4, 18, 255, 106, 216, 159, 163, 49, 221, 87, 133, 251, 155, 76, 100, 231, 89, 77, 177, 133, 231, 168, 51, 198, 58, 209, 153, 96, 52, 220, 212, 86, 244, 72, 10, 201, 184, 4, 226, 198, 118 };

            var expectedSharedKey = new byte[] { 0, 158, 251, 248, 227, 69, 237, 180, 214, 4, 221, 192, 22, 179, 47, 147, 26, 235, 107, 104, 0, 212, 11, 128, 240, 45, 233, 103, 194, 72, 176, 18 };
            var expectedDecryptedPayload = new byte[] { 18, 8, 219, 37, 78, 203, 52, 33, 59, 229, 26, 3, 1, 2, 3 };

            var actualSharedKey = key.Decrypt(encryptedKey, nonce.Take(Hash.NonceLength).ToArray(), sourcePublicKey);
            var actualDecryptedPayload = Hash.DecryptSymmetric(rawPayload, nonce.Skip(Hash.NonceLength).ToArray(), actualSharedKey);
            
            Assert.Equal(expectedSharedKey, actualSharedKey);
            Assert.Equal(expectedDecryptedPayload, actualDecryptedPayload);
        }

        [Fact]
        public void Should_Encrypt_Correctly()
        {
            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);

        }
    }
}
