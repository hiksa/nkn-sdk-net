using System.Collections.Generic;

using Xunit;

using NknSdk.Common.Extensions;

namespace NknSdk.Tests.Common
{
    public class HexEncodingTests
    {
        [Fact]
        public void EncodeByte()
        {
            var inputs = new List<byte> { 0, 32 };
            var expectedResults = new List<string> { "00", "20" };

            for (int i = 0; i < inputs.Count; i++)
            {
                var result = inputs[i].EncodeHex();
                Assert.Equal(expectedResults[i], result);
            }
        }

        [Fact]
        public void EncodeShort()
        {

        }

        [Fact]
        public void EncodeInt()
        {
            var inputs = new List<int> { 77, 757730019 };
            var expectedResults = new List<string> { "4d000000", "e30a2a2d" };

            for (int i = 0; i < inputs.Count; i++)
            {
                var result = inputs[i].EncodeHex();
                Assert.Equal(expectedResults[i], result);
            }
        }

        [Fact]
        public void EncodeUint()
        {

        }

        [Fact]
        public void EncodeLong()
        {

        }

        [Fact]
        public void EncodeUlong()
        {

        }

        [Fact]
        public void EncodeByteArray()
        {

        }

        [Fact]
        public void EncodeBool()
        {

        }
    }
}
