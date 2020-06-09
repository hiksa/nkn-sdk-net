using Ncp.Protobuf;
using NknSdk.Client;
using NknSdk.Common;
using NknSdk.Common.Extensions;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.SignatureChain;
using System.Collections.Generic;
using System.Linq;
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
                Message = new byte[] { 120, 156, 99, 4, 0, 0, 2, 0, 2 }
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

            var encoded = element.EncodeHex();

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

            var encoded = metaData.EncodeHex();

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
                IsHandshake = true,
                WindowSize = recieveWindowSize,
                Mtu = recieveMtu
            };

            var result = ProtoSerializer.Serialize(packet);

            Assert.Equal(expectedBuffer, result);
        }

        [Fact]
        public void MessageShouldCompressCorrectly()
        {
            var message = new byte[] { 26, 70, 95, 95, 48, 95, 95, 46, 102, 99, 54, 99, 50, 51, 49, 57, 49, 53, 100, 53, 54, 56, 102, 53, 48, 101, 52, 100, 97, 99, 50, 56, 53, 56, 101, 56, 51, 51, 101, 99, 55, 102, 99, 54, 99, 50, 55, 101, 57, 99, 100, 48, 52, 48, 99, 101, 100, 48, 56, 102, 57, 52, 49, 101, 53, 99, 56, 97, 51, 50, 102, 55, 26, 70, 95, 95, 48, 95, 95, 46, 97, 52, 97, 53, 100, 49, 53, 50, 102, 56, 51, 102, 101, 56, 56, 48, 50, 98, 97, 49, 51, 50, 57, 101, 100, 51, 101, 51, 49, 97, 97, 50, 102, 51, 52, 57, 50, 102, 99, 49, 97, 55, 55, 53, 101, 101, 48, 50, 102, 52, 49, 57, 50, 55, 49, 48, 51, 102, 48, 99, 99, 48, 56, 56, 40, 1, 50, 32, 241, 207, 167, 79, 180, 215, 120, 195, 46, 112, 132, 51, 237, 249, 248, 4, 30, 239, 211, 35, 112, 114, 57, 69, 187, 81, 90, 235, 123, 91, 117, 75, 58, 64, 175, 108, 22, 91, 253, 246, 255, 51, 82, 225, 52, 134, 212, 102, 69, 21, 139, 162, 246, 174, 144, 187, 101, 47, 104, 62, 91, 168, 201, 57, 202, 145, 224, 2, 95, 90, 52, 20, 96, 11, 181, 3, 128, 217, 232, 233, 183, 241, 203, 8, 10, 26, 197, 142, 142, 135, 76, 254, 64, 191, 73, 209, 35, 5, 58, 64, 141, 181, 228, 0, 42, 211, 126, 89, 204, 6, 137, 130, 83, 38, 75, 75, 70, 97, 152, 9, 196, 69, 170, 152, 237, 189, 14, 190, 212, 56, 175, 67, 195, 15, 109, 244, 207, 12, 83, 236, 179, 123, 198, 237, 215, 174, 96, 30, 209, 186, 92, 194, 227, 243, 43, 103, 6, 151, 118, 3, 4, 180, 179, 2, 66, 135, 1, 10, 31, 92, 254, 101, 113, 190, 174, 55, 58, 26, 36, 86, 1, 2, 78, 19, 226, 243, 206, 222, 170, 253, 11, 202, 61, 236, 25, 24, 39, 227, 190, 188, 16, 1, 26, 48, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 34, 48, 247, 229, 144, 108, 87, 131, 38, 87, 94, 236, 0, 19, 243, 99, 242, 42, 117, 113, 25, 15, 65, 85, 162, 88, 193, 148, 236, 121, 102, 210, 118, 52, 16, 103, 248, 68, 56, 105, 138, 179, 46, 143, 170, 60, 40, 88, 76, 38, 66, 135, 1, 10, 31, 92, 254, 101, 113, 190, 174, 55, 58, 26, 36, 86, 1, 2, 78, 19, 226, 243, 206, 222, 170, 253, 11, 202, 61, 236, 25, 24, 39, 227, 190, 188, 16, 1, 26, 48, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 34, 48, 156, 216, 255, 224, 11, 84, 129, 149, 178, 164, 192, 212, 145, 21, 110, 114, 169, 92, 84, 228, 188, 159, 78, 187, 135, 1, 174, 213, 207, 14, 83, 229, 215, 84, 116, 202, 44, 179, 250, 68, 75, 84, 50, 244, 211, 213, 95, 66 };

            var expected = new byte[] { 120, 156, 147, 114, 139, 143, 55, 136, 143, 215, 75, 75, 54, 75, 54, 50, 54, 180, 52, 52, 77, 49, 53, 179, 72, 51, 53, 72, 53, 73, 73, 76, 54, 178, 48, 181, 72, 181, 48, 54, 78, 77, 54, 7, 43, 48, 79, 181, 76, 78, 49, 48, 49, 72, 78, 77, 49, 176, 72, 179, 52, 49, 76, 53, 77, 182, 72, 52, 54, 74, 51, 151, 130, 154, 147, 104, 146, 104, 154, 98, 104, 106, 148, 102, 97, 156, 150, 106, 97, 97, 96, 148, 148, 104, 104, 108, 100, 153, 154, 98, 156, 106, 108, 152, 152, 104, 148, 102, 108, 98, 105, 148, 150, 108, 152, 104, 110, 110, 154, 154, 106, 96, 148, 102, 98, 104, 105, 100, 110, 104, 96, 156, 102, 144, 156, 108, 96, 97, 161, 193, 104, 164, 240, 241, 252, 114, 255, 45, 215, 43, 14, 235, 21, 180, 24, 191, 253, 249, 131, 69, 238, 253, 101, 229, 130, 34, 75, 215, 221, 129, 81, 175, 171, 163, 75, 189, 173, 28, 214, 231, 136, 69, 255, 253, 246, 223, 56, 232, 161, 73, 219, 149, 52, 87, 209, 238, 69, 223, 214, 77, 216, 157, 170, 159, 97, 23, 189, 226, 164, 229, 169, 137, 15, 152, 226, 163, 76, 68, 18, 184, 183, 50, 55, 220, 124, 241, 114, 251, 199, 211, 28, 92, 82, 71, 251, 250, 218, 125, 254, 57, 236, 247, 188, 168, 204, 106, 229, 208, 187, 245, 9, 131, 214, 229, 186, 200, 51, 108, 157, 77, 193, 106, 222, 222, 110, 137, 51, 56, 143, 184, 174, 154, 241, 118, 47, 223, 190, 43, 22, 235, 157, 15, 243, 231, 126, 57, 207, 19, 252, 102, 115, 245, 177, 183, 215, 215, 37, 200, 93, 220, 21, 115, 232, 241, 103, 237, 116, 182, 233, 101, 204, 44, 91, 54, 51, 57, 181, 51, 114, 201, 199, 252, 75, 45, 220, 183, 206, 220, 74, 74, 37, 140, 145, 201, 79, 248, 209, 231, 115, 247, 86, 253, 229, 62, 101, 251, 70, 82, 66, 253, 241, 190, 61, 2, 140, 82, 6, 140, 36, 2, 37, 131, 239, 79, 39, 228, 132, 55, 171, 133, 199, 189, 97, 16, 254, 156, 252, 73, 171, 180, 80, 146, 223, 49, 116, 81, 196, 193, 41, 111, 42, 211, 46, 149, 153, 8, 164, 255, 112, 177, 200, 236, 218, 172, 215, 191, 202, 70, 35, 194, 71, 141, 118, 46, 153, 115, 227, 255, 3, 238, 144, 198, 169, 155, 150, 28, 184, 50, 81, 52, 175, 104, 101, 76, 200, 147, 61, 243, 253, 118, 183, 51, 174, 187, 122, 158, 47, 248, 233, 245, 144, 146, 83, 58, 155, 127, 185, 120, 135, 24, 125, 185, 124, 53, 222, 9, 0, 190, 182, 202, 190 };

            var result = message.Compress();

            Assert.Equal(expected, result);
        }

        //[Fact]
        //public void MessageShouldDecompressCorrectly()
        //{
        //    var message = new byte[] { 1, 2, 3, 4, 5 };

        //    var expected = new byte[] { };

        //    var result = message.Decompress();

        //    //Assert.Equal(expected, result);
        //}
    }
}
