using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Ncp.Protobuf;
using NknSdk.Client;
using NknSdk.Common;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.SignatureChain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using ProtoSerializer = NknSdk.Common.Protobuf.ProtoSerializer;

namespace NknSdk.Tests.Client
{
    public class MessageFactoryTests
    {
        [Fact]
        public void Payload_Should_ConstructCorrectly()
        {
            var replyToId = "";
            var messageId = "";
            var textPayloadData = "test";
            var binaryPayloadData = new byte[] { 1, 2, 3, 4, 5, 100 };

            var actualBinaryPayload = MessageFactory.MakeBinaryPayload(binaryPayloadData, replyToId, messageId);
            var actualTextPayload = MessageFactory.MakeTextPayload(textPayloadData, replyToId, messageId);



        }

        [Fact]
        public void ClientMessage_Should_ConstructCorrectly()
        {
            var message = new byte[] { 1 };
            var expectedMessage = new ClientMessage
            {
                Type = ClientMessageType.InboundMessage,
                CompressionType = null,
                Message = message
            };

            var constructedMessage = MessageFactory.MakeClientMessage(
                ClientMessageType.InboundMessage,
                message,
                CompressionType.None);

            Assert.Equal(expectedMessage.Type, constructedMessage.Type);
            Assert.Equal(expectedMessage.Message, constructedMessage.Message);
            Assert.Equal(expectedMessage.CompressionType, constructedMessage.CompressionType);

            var expectedCompressed = new ClientMessage
            {
                Type = ClientMessageType.InboundMessage,
                CompressionType = CompressionType.Zlib,
                Message = message
            };

            var constructedCompressed = MessageFactory.MakeClientMessage(ClientMessageType.InboundMessage, message, CompressionType.Zlib);

            Assert.Equal(expectedCompressed.Type, constructedCompressed.Type);
            Assert.Equal(expectedCompressed.CompressionType, constructedCompressed.CompressionType);
            Assert.Equal(expectedCompressed.Message, constructedCompressed.Message);
        }

        [Fact]
        public void SerializeChainElement_Should_CalculateCorrect()
        {
            var element = new SignatureChainElement
            {
                NextPublicKey = new byte[] { 207, 187, 193, 16, 186, 18, 240, 244, 135, 48, 151, 157, 148, 199, 204, 93, 205, 158, 76, 2, 80, 245, 151, 223, 11, 202, 182, 205, 63, 17, 132, 196 }
            };

            var expectedHex = "0020cfbbc110ba12f0f48730979d94c7cc5dcd9e4c0250f597df0bcab6cd3f1184c400";

            var encoded = MessageFactory.EncodeSignatureChainElement(element);

            Assert.Equal(expectedHex, encoded);
        }

        [Fact]
        public void SerializeSignatureCHainMetadata_Should_CalculateCorrect()
        {
            var metaData = new SignatureChain
            {
                Nonce = 1,
                DataSize = 77,
                BlockHash = new byte[] { 183, 204, 123, 181, 107, 41, 227, 137, 187, 255, 183, 99, 80, 127, 224, 136, 149, 58, 151, 82, 73, 169, 58, 171, 116, 75, 72, 207, 247, 141, 251, 8 },
                SourceId = new byte[] { 96, 31, 206, 156, 4, 208, 139, 102, 57, 59, 182, 104, 35, 5, 12, 0, 191, 183, 61, 213, 119, 208, 185, 107, 155, 139, 49, 114, 102, 53, 190, 197 },
                SourcePublicKey = new byte[] { 164, 165, 209, 82, 248, 63, 232, 128, 43, 161, 50, 158, 211, 227, 26, 162, 243, 73, 47, 193, 167, 117, 238, 2, 244, 25, 39, 16, 63, 12, 192, 136 },
                DestinationId = new byte[] { 67, 86, 205, 17, 80, 238, 132, 114, 233, 170, 72, 9, 239, 72, 73, 182, 61, 224, 37, 27, 22, 204, 51, 104, 31, 162, 43, 85, 34, 215, 137, 2 },
                DestinationPublicKey = new byte[] { 252, 108, 35, 25, 21, 213, 104, 245, 14, 77, 172, 40, 88, 232, 51, 236, 127, 198, 194, 126, 156, 208, 64, 206, 208, 143, 148, 30, 92, 138, 50, 247 },
                Elements = new List<SignatureChainElement>()
            };

            var expectedHex = "010000004d00000020b7cc7bb56b29e389bbffb763507fe088953a975249a93aab744b48cff78dfb0820601fce9c04d08b66393bb66823050c00bfb73dd577d0b96b9b8b31726635bec520a4a5d152f83fe8802ba1329ed3e31aa2f3492fc1a775ee02f41927103f0cc088204356cd1150ee8472e9aa4809ef4849b63de0251b16cc33681fa22b5522d7890220fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";

            var encoded = MessageFactory.EncodeSignatureChainMetadata(metaData);

            Assert.Equal(expectedHex, encoded);
        }

        [Fact]
        public void Receipt_Should_ConstructCorrectly()
        {
            var rawMessage = new byte[] { 10, 64, 102, 99, 54, 99, 50, 51, 49, 57, 49, 53, 100, 53, 54, 56, 102, 53, 48, 101, 52, 100, 97, 99, 50, 56, 53, 56, 101, 56, 51, 51, 101, 99, 55, 102, 99, 54, 99, 50, 55, 101, 57, 99, 100, 48, 52, 48, 99, 101, 100, 48, 56, 102, 57, 52, 49, 101, 53, 99, 56, 97, 51, 50, 102, 55, 18, 64, 10, 34, 161, 78, 116, 148, 173, 232, 183, 255, 216, 195, 68, 213, 30, 251, 82, 174, 235, 134, 145, 174, 31, 57, 181, 175, 128, 39, 28, 31, 68, 78, 185, 233, 72, 154, 16, 1, 26, 24, 192, 116, 161, 96, 87, 204, 81, 215, 97, 12, 149, 32, 238, 221, 207, 171, 202, 188, 81, 85, 219, 238, 230, 194, 26, 32, 220, 34, 34, 188, 221, 88, 139, 146, 216, 21, 73, 167, 73, 148, 185, 238, 216, 191, 209, 69, 234, 227, 120, 40, 166, 97, 255, 77, 199, 101, 230, 93 };

            var expectedInboundMessage = new InboundMessage
            {
                Source = "fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7",
                PreviousSignature = new byte[] { 220, 34, 34, 188, 221, 88, 139, 146, 216, 21, 73, 167, 73, 148, 185, 238, 216, 191, 209, 69, 234, 227, 120, 40, 166, 97, 255, 77, 199, 101, 230, 93 },
                Payload = new byte[] { 10, 34, 161, 78, 116, 148, 173, 232, 183, 255, 216, 195, 68, 213, 30, 251, 82, 174, 235, 134, 145, 174, 31, 57, 181, 175, 128, 39, 28, 31, 68, 78, 185, 233, 72, 154, 16, 1, 26, 24, 192, 116, 161, 96, 87, 204, 81, 215, 97, 12, 149, 32, 238, 221, 207, 171, 202, 188, 81, 85, 219, 238, 230, 194 }
            };

            var expectedReceipt = new ClientMessage
            {
                Type = ClientMessageType.Receipt,
                Message = new byte[] { 10, 32, 220, 34, 34, 188, 221, 88, 139, 146, 216, 21, 73, 167, 73, 148, 185, 238, 216, 191, 209, 69, 234, 227, 120, 40, 166, 97, 255, 77, 199, 101, 230, 93, 18, 64, 197, 48, 7, 228, 24, 1, 53, 234, 10, 7, 130, 121, 216, 89, 195, 147, 11, 125, 212, 19, 203, 108, 89, 74, 50, 168, 100, 137, 159, 173, 67, 147, 207, 112, 36, 181, 57, 172, 208, 65, 40, 237, 193, 249, 116, 191, 254, 137, 217, 216, 175, 22, 232, 19, 134, 251, 185, 231, 194, 57, 164, 28, 132, 1 },
                CompressionType = CompressionType.None
            };

            var expectedReceiptBinary = new byte[] { 8, 2, 18, 100, 10, 32, 220, 34, 34, 188, 221, 88, 139, 146, 216, 21, 73, 167, 73, 148, 185, 238, 216, 191, 209, 69, 234, 227, 120, 40, 166, 97, 255, 77, 199, 101, 230, 93, 18, 64, 197, 48, 7, 228, 24, 1, 53, 234, 10, 7, 130, 121, 216, 89, 195, 147, 11, 125, 212, 19, 203, 108, 89, 74, 50, 168, 100, 137, 159, 173, 67, 147, 207, 112, 36, 181, 57, 172, 208, 65, 40, 237, 193, 249, 116, 191, 254, 137, 217, 216, 175, 22, 232, 19, 134, 251, 185, 231, 194, 57, 164, 28, 132, 1 };

            var seed = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var key = new CryptoKey(seed);

            var inboundMessage = rawMessage.FromBytes<InboundMessage>();

            Assert.Equal(expectedInboundMessage.Source, inboundMessage.Source);
            Assert.Equal(expectedInboundMessage.Payload, inboundMessage.Payload);
            Assert.Equal(expectedInboundMessage.PreviousSignature, inboundMessage.PreviousSignature);

            var previousSignatureHex = inboundMessage.PreviousSignature.ToHexString();

            var constructedReceipt = MessageFactory.MakeReceipt(key, previousSignatureHex);

            Assert.Equal(expectedReceipt.Message, constructedReceipt.Message);

            var constructedReceiptBytes = constructedReceipt.ToBytes();

            Assert.Equal(expectedReceiptBinary, constructedReceiptBytes);
        }

        [Fact]
        public void HandshakePacket_Should_ConstructCorrectly()
        {
            var localClientIds = new string[] { "__0__" };
            uint recieveWindowSize = 4194304;
            uint recieveMtu = 1024;

            var expectedBuffer = new byte[] { 50, 5, 95, 95, 48, 95, 95, 56, 128, 128, 128, 2, 64, 128, 8, 80, 1 };

            var packet = new Packet
            {
                ClientIds = localClientIds.ToList(),
                Handshake = true,
                WindowSize = recieveWindowSize,
                Mtu = recieveMtu
            };

            var result = NknSdk.Common.Protobuf.ProtoSerializer.Serialize(packet);

            Assert.Equal(expectedBuffer, result);
        }

        [Fact]
        public void Test()
        {
            var seed3 = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var clientOptions = MultiClientOptions.Default;
            clientOptions.Seed = seed3;
            clientOptions.NumberOfSubClients = 1;
            var destination = "__0__.fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";
            var client = new MultiClient(clientOptions);

            client.Connected += (sender, args) =>
            {
                var localClientIds = new string[] { "__0__" };
                uint recieveWindowSize = 4194304;
                uint recieveMtu = 1024;
                var sessionId = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 };

                var expectedBuffer = new byte[] { 50, 5, 95, 95, 48, 95, 95, 56, 128, 128, 128, 2, 64, 128, 8, 80, 1 };

                var packet = new Packet { ClientIds = localClientIds.ToList(), Handshake = true, WindowSize = recieveWindowSize, Mtu = recieveMtu };

                var packetBuffer = ProtoSerializer.Serialize(packet);

                Assert.Equal(expectedBuffer, packetBuffer);

                var payload = MessageFactory.MakeSessionPayload(packetBuffer, sessionId.ToHexString());
                var serializedPayload = ProtoSerializer.Serialize(payload);

                var expectedSerializedPayload = new byte[] { 8, 3, 18, 8, 1, 1, 1, 1, 1, 1, 1, 1, 26, 17, 50, 5, 95, 95, 48, 95, 95, 56, 128, 128, 128, 2, 64, 128, 8, 80, 1 };

                Assert.Equal(expectedSerializedPayload, serializedPayload);

                var message = client.clients.FirstOrDefault().Value.MakeMessageFromPayload(payload, true, destination);
                var serializedMessage = message.ToBytes();

                var expectedSerializedMessage = new byte[] { 10, 47, 227, 184, 222, 45, 86, 196, 10, 163, 144, 107, 122, 23, 92, 235, 65, 103, 214, 15, 42, 54, 230, 4, 84, 250, 92, 193, 70, 155, 105, 154, 73, 235, 216, 32, 101, 198, 181, 64, 177, 65, 222, 209, 147, 64, 126, 86, 121, 16, 1, 26, 24, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

                Assert.Equal(expectedSerializedMessage, serializedMessage);

                var outboundMessage = MessageFactory.MakeOutboundMessage(
                    client.clients.FirstOrDefault().Value,
                    new List<string> { destination },
                    new byte[][] { serializedMessage },
                    0);

                var data = outboundMessage.ToBytes();

                var expectedData = new byte[] { 18, 253, 1, 26, 70, 95, 95, 48, 95, 95, 46, 102, 99, 54, 99, 50, 51, 49, 57, 49, 53, 100, 53, 54, 56, 102, 53, 48, 101, 52, 100, 97, 99, 50, 56, 53, 56, 101, 56, 51, 51, 101, 99, 55, 102, 99, 54, 99, 50, 55, 101, 57, 99, 100, 48, 52, 48, 99, 101, 100, 48, 56, 102, 57, 52, 49, 101, 53, 99, 56, 97, 51, 50, 102, 55, 40, 1, 50, 32, 144, 154, 221, 87, 15, 134, 154, 194, 63, 228, 232, 53, 56, 237, 18, 163, 32, 196, 103, 14, 158, 49, 197, 177, 5, 181, 249, 45, 183, 102, 126, 47, 58, 64, 237, 198, 184, 134, 243, 245, 178, 5, 88, 193, 147, 14, 238, 28, 237, 26, 115, 200, 74, 14, 204, 131, 91, 33, 59, 235, 43, 221, 33, 105, 34, 190, 102, 90, 70, 67, 191, 24, 161, 230, 107, 238, 43, 200, 93, 2, 69, 191, 20, 62, 171, 181, 181, 231, 101, 86, 160, 106, 43, 170, 20, 124, 196, 3, 66, 77, 10, 47, 227, 184, 222, 45, 86, 196, 10, 163, 144, 107, 122, 23, 92, 235, 65, 103, 214, 15, 42, 54, 230, 4, 84, 250, 92, 193, 70, 155, 105, 154, 73, 235, 216, 32, 101, 198, 181, 64, 177, 65, 222, 209, 147, 64, 126, 86, 121, 16, 1, 26, 24, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

                for (int i = 0; i < data.Length; i++)
                {
                    Assert.Equal(expectedData[i], data[i]);
                }

                Assert.Equal(expectedData, data);

            };

            Console.ReadLine();
        }

        private void Client_Connected(object sender, EventArgs e)
        {
            Console.WriteLine("Connected");
        }

        [Fact]
        public void SessionPayloadEncrypted_Should_ConstructCorrectly()
        {
            var seed3 = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var client = new NknSdk.Client.Client(new ClientOptions { Seed = seed3, Encrypt = true });
            var localClientIds = new string[] { "__0__" };
            uint recieveWindowSize = 4194304;
            uint recieveMtu = 1024;
            var destination = "__0__.fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";
            var expectedBuffer = new byte[] { 50, 5, 95, 95, 48, 95, 95, 56, 128, 128, 128, 2, 64, 128, 8, 80, 1 };
            var packet = new Packet
            {
                ClientIds = localClientIds.ToList(),
                Handshake = true,
                WindowSize = recieveWindowSize,
                Mtu = recieveMtu
            };

            var sessionId = new byte[] { 155, 212, 115, 63, 123, 70, 49, 12 };

            var buffer = NknSdk.Common.Protobuf.ProtoSerializer.Serialize(packet);
            Assert.Equal(expectedBuffer, buffer);

            var payload = MessageFactory.MakeSessionPayload(buffer, sessionId.ToHexString());

            var message = client.MakeMessageFromPayload(payload, true, destination);
            var messageBytes = ProtoSerializer.Serialize(message);

            var expectedPldMsg = new byte[] { 10, 47, 85, 213, 142, 53, 241, 186, 238, 78, 83, 252, 26, 207, 158, 159, 192, 42, 214, 15, 42, 54, 124, 209, 38, 196, 38, 134, 118, 150, 105, 154, 73, 235, 216, 32, 101, 198, 181, 64, 177, 65, 222, 209, 147, 64, 126, 86, 121, 16, 1, 26, 24, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            Assert.Equal(expectedPldMsg, messageBytes);
        }
    }
}
