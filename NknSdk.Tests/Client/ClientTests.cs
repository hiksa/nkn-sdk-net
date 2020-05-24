using NknSdk.Client;
using NknSdk.Common;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Payloads;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NknSdk.Tests.Client
{
    public class ClientTests
    {
        [Fact]
        public void MakeMessageFromPayload_Should_ConstructCorrectMessageUnencrypted()
        {
            var seed3 = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var client = new NknSdk.Client.Client(new ClientOptions { Seed = seed3, Encrypt = true });

            var payloadData = new byte[] { 50, 5, 95, 95, 48, 95, 95, 56, 128, 128, 128, 2, 64, 128, 8, 80, 1 };
            var sessionId = new byte[] { 158, 155, 57, 149, 252, 87, 26, 67 };
            var payload = MessageFactory.MakeSessionPayload(payloadData, sessionId.ToHexString());
            var payloadBytes = new byte[] { 8, 3, 18, 8, 158, 155, 57, 149, 252, 87, 26, 67, 26, 17, 50, 5, 95, 95, 48, 95, 95, 56, 128, 128, 128, 2, 64, 128, 8, 80, 1 };

            var isEncrypted = true;
            var destination = "__0__.fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";
            var expectedMessageBytes = new byte[] { 10, 47, 169, 40, 11, 225, 163, 238, 14, 46, 117, 31, 129, 226, 208, 4, 95, 138, 214, 34, 57, 24, 48, 245, 101, 178, 244, 63, 83, 46, 167, 136, 158, 114, 58, 90, 160, 94, 228, 172, 164, 193, 211, 212, 14, 5, 123, 30, 187, 16, 1, 26, 24, 110, 34, 146, 168, 209, 59, 84, 120, 1, 191, 224, 14, 15, 48, 235, 231, 247, 192, 71, 4, 11, 176, 149, 9 };

            var serialized = payload.ToBytes();

            Assert.Equal(payloadBytes, serialized);

            var message = client.MakeMessageFromPayload(payload, true, destination);
            var messageBytes = ProtoSerializer.Serialize(message);

            //    Assert.Equal(expectedSerializedPayload, serialized);
            Assert.Equal(expectedMessageBytes, messageBytes);
        }

        [Fact]
        public void MakeMessageFromPayload_Should_ConstructCorrectMessageEncrypted()
        {

        }
    }
}
