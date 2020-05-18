using NknSdk.Client;
using NknSdk.Common;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.SignatureChain;
using Xunit;

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

            };

            var chainElementBytes = MessageFactory.EncodeSignatureChainElement(element);


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
    }
}
