using System.Collections.Generic;
using System.Linq;

using NknSdk.Common;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Extensions;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.Payloads;
using NknSdk.Common.Protobuf.SignatureChain;

namespace NknSdk.Client
{
    public static class MessageFactory
    {
        public static Payload MakePayload(PayloadType type, string replyToId, byte[] data, string messageId)
        {
            var payload = new Payload { Type = type, Data = data };

            if (!string.IsNullOrWhiteSpace(replyToId))
            {
                payload.ReplyToId = replyToId.FromHexString();
            }
            else if (!string.IsNullOrWhiteSpace(messageId))
            {
                payload.MessageId = messageId.FromHexString();
            }
            else
            {
                payload.MessageId = PseudoRandom.RandomBytes(Constants.MessageIdLength);
            }

            return payload;
        }

        public static Payload MakeBinaryPayload(byte[] data, string replyToId, string messageId)
        {
            return MessageFactory.MakePayload(PayloadType.Binary, replyToId, data, messageId);
        }

        public static Payload MakeTextPayload(string text, string replyToId, string messageId)
        {
            var textDataPayload = new TextDataPayload { Text = text };
            var data = ProtoSerializer.Serialize(textDataPayload);

            return MessageFactory.MakePayload(PayloadType.Text, replyToId, data, messageId);
        }

        public static Payload MakeAckPayload(string replyToId, string messageId)
        {
            return MessageFactory.MakePayload(PayloadType.Ack, replyToId, null, messageId);
        }

        public static Payload MakeSessionPayload(byte[] data, string sessionId)
        {
            return MessageFactory.MakePayload(PayloadType.Session, null, data, sessionId);
        }

        public static MessagePayload MakeMessage(
            byte[] payload,
            bool encrypted,
            byte[] nonce = null,
            byte[] encryptedKey = null)
        {
            var message = new MessagePayload
            {
                Payload = payload,
                IsEncrypted = encrypted,
                Nonce = nonce,
                EncryptedKey = encryptedKey
            };

            return message;
        }

        public static ClientMessage MakeClientMessage(
            ClientMessageType type,
            byte[] message,
            CompressionType compressionType)
        {
            var clientMessage = new ClientMessage
            {
                Type = type == ClientMessageType.OutboundMessage ? default(ClientMessageType?) : type,
                Message = message,
                CompressionType = compressionType == CompressionType.None ? default(CompressionType?) : compressionType
            };

            switch (compressionType)
            {
                case CompressionType.None:
                    break;
                case CompressionType.Zlib:
                    clientMessage.Message = message.Compress();
                    break;
                default:
                    throw new InvalidArgumentException($"unknown compression type {compressionType}");
            }

            return clientMessage;
        }

        public static ClientMessage MakeOutboundMessage(
            Client client,
            IList<string> destinations,
            IList<byte[]> payloads,
            uint maxHoldingSeconds)
        {
            if (destinations == null || destinations.Count() == 0)
            {
                throw new InvalidArgumentException("No destination");
            }

            if (payloads == null || payloads.Count() == 0)
            {
                throw new InvalidArgumentException("No payloads");
            }

            if (payloads.Count() > 1 && payloads.Count() != destinations.Count())
            {
                throw new InvalidArgumentException("Invalid payloads count");
            }

            var signatureChainElement = new SignatureChainElement();
            signatureChainElement.NextPublicKey = client.RemoteNode.Publickey.FromHexString();

            var signatureChainElementHexEncoded = signatureChainElement.EncodeHex();

            var signatureChain = new SignatureChain
            {
                Nonce = (uint)PseudoRandom.RandomInt(),
                SourceId = Address.AddressToId(client.Address).FromHexString(),
                SourcePublicKey = client.PublicKey.FromHexString()
            };

            if (!string.IsNullOrWhiteSpace(client.SignatureChainBlockHash))
            {
                signatureChain.BlockHash = client.SignatureChainBlockHash.FromHexString();
            }

            var signatures = new List<byte[]>();

            for (int i = 0; i < destinations.Count; i++)
            {
                signatureChain.DestinationId = Address.AddressToId(destinations[i]).FromHexString();
                signatureChain.DestinationPublicKey = Address.AddressToPublicKey(destinations[i]).FromHexString();

                if (payloads.Count > 1)
                {
                    signatureChain.DataSize = (uint)payloads[i].Length;
                }
                else
                {
                    signatureChain.DataSize = (uint)payloads[0].Length;
                }

                var hex = signatureChain.EncodeHex();

                var digest = Hash.Sha256Hex(hex);
                digest = Hash.Sha256Hex(digest + signatureChainElementHexEncoded);

                var signature = client.Key.Sign(digest.FromHexString());
                signatures.Add(signature);
            }

            var message = new OutboundMessage
            {
                Destinations = destinations.ToArray(),
                Payloads = payloads.ToArray(),
                MaxHoldingSeconds = maxHoldingSeconds,
                Nonce = signatureChain.Nonce,
                BlockHash = signatureChain.BlockHash,
                Signatures = signatures.ToArray()
            };

            var compressionType = payloads.Count > 1
                ? CompressionType.Zlib
                : CompressionType.None;

            var messageBytes = message.ToBytes();

            return MessageFactory.MakeClientMessage(ClientMessageType.OutboundMessage, messageBytes, compressionType);
        }

        public static ClientMessage MakeReceipt(CryptoKey key, string previousSignatureHex)
        {
            var signatureChainElement = new SignatureChainElement();
            var signatureChainElementHexEncoded = signatureChainElement.EncodeHex();

            var digest = Hash.Sha256Hex(previousSignatureHex);
            digest = Hash.Sha256Hex(digest + signatureChainElementHexEncoded);

            var signature = key.Sign(digest.FromHexString());

            var receipt = new Receipt
            {
                PreviousSignature = previousSignatureHex.FromHexString(),
                Signature = signature
            };

            var receiptBytes = receipt.ToBytes();

            return MessageFactory.MakeClientMessage(ClientMessageType.Receipt, receiptBytes, CompressionType.None);
        }
    }
}
