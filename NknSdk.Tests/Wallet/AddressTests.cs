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

            var result = Address.IsCorrectAddress(address);

            Assert.True(result);
        }
    }
}
