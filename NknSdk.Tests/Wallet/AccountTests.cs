using Xunit;

using NknSdk.Wallet.Models;

namespace NknSdk.Tests.Wallet
{
    public class AccountTests
    {
        [Fact]
        public void AllProperties()
        {
            var seed = "16735a849deaa136ba6030c3695c4cbdc9b275d5d9a9f46b836841ab4a36179e";
            var account = new Account(seed);

            var expectedAddress = "NKNLH46GEreBTEQqZsUcoeiFeTAHEuPGWshw";
            var expectedContract = "22207ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9ac01006071b3b89bf3afcb337d278919d154ecc231e913";
            var expectedProgramHash = "6071b3b89bf3afcb337d278919d154ecc231e913";
            var expectedPublicKey = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";
            var expectedSignatureRedeem = "207ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9ac";

            Assert.Equal(expectedAddress, account.Address);
            Assert.Equal(expectedContract, account.Contract);
            Assert.Equal(expectedProgramHash, account.ProgramHash);
            Assert.Equal(expectedPublicKey, account.PublicKey);
            Assert.Equal(expectedSignatureRedeem, account.SignatureRedeem);
            Assert.Equal(seed, account.Seed);
        }

        [Fact]
        public void ShouldSignCorrect()
        {
            var seed = "b71a349865ed5907821e903389e83424d037d4e1680935fd3c1f33408df2fdf5";
            var account = new Account(seed);
            var message = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
            var expected = new byte[] { 236, 119, 93, 24, 178, 69, 106, 100, 127, 34, 121, 209, 112, 125, 219, 120, 101, 9, 31, 112, 175, 65, 25, 128, 203, 66, 195, 146, 101, 41, 185, 212, 113, 71, 114, 231, 6, 187, 202, 14, 95, 4, 174, 86, 231, 53, 215, 231, 125, 18, 194, 110, 242, 155, 216, 253, 89, 118, 240, 235, 73, 64, 69, 5 };

            var signature = account.Sign(message);

            Assert.Equal(expected, signature);
        }
    }
}
