using NknSdk.Wallet;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace NknSdk.Tests.Wallet
{
    public class AccountTests
    {
        [Fact]
        public void AllProperties()
        {
            var seed = "b71a349865ed5907821e903389e83424d037d4e1680935fd3c1f33408df2fdf5";
            var account = new Account(seed);

            var expectedAddress = "2myqRmzZA9A6CQ827oko8gNnJ9H7pcHbWR4tU3SVNndNLxgXQcGR5";
            var expectedContract = "22203fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6eac0100d86152d751795bd883fce02b9331c111db149ab6a20d751beaa99111c6295d14";
            var expectedProgramHash = "d86152d751795bd883fce02b9331c111db149ab6a20d751beaa99111c6295d14";
            var expectedPublicKey = "3fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6e";
            var expectedSignatureRedeem = "203fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6eac";

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
