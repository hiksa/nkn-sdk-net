using NknSdk.Client;
using NknSdk.Common;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.SignatureChain;
using NknSdk.Wallet;
using System;
using Xunit;

namespace NknSdk.Tests.Wallet
{
    public class AddressTests
    {
        [Fact]
        public void AddressIsCorrect()
        {
            var address = "NKNZw5tHypCKCGSuuxbX2LUV8X2f95gd8WjE";

            var result = Address.Verify(address);

            Assert.True(result);
        }

        [Fact]
        public void HexStringToProgramHash()
        {
            var hexString = "207ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9ac";

            var expected = "6071b3b89bf3afcb337d278919d154ecc231e913";

            var result = Address.HexStringToProgramHash(hexString);

            Assert.Equal(expected, result);
        }
    }
}
