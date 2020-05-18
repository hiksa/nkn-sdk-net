using NknSdk.Common;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.Payloads;
using NknSdk.Common.Protobuf.SignatureChain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NknSdk.Client
{
    public static class MessageFactory
    {
        public static Payload MakePayload(PayloadType type, string replyToId, byte[] data, string messageId)
        {
            var payload = new Payload { Type = type };

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

            payload.Data = data;

            return payload;
        }

        public static Payload MakeBinaryPayload(byte[] data, string replyToId, string messageId)
            => MakePayload(PayloadType.Binary, replyToId, data, messageId);

        public static Payload MakeTextPayload(string text, string replyToId, string messageId)
        {
            var textDataPayload = new TextDataPayload { Text = text };
            var data = ProtoSerializer.Serialize(textDataPayload);

            return MakePayload(PayloadType.Text, replyToId, data, messageId);
        }

        public static Payload MakeAckPayload(string replyToId, string messageId)
            => MakePayload(PayloadType.Ack, replyToId, null, messageId);

        public static Payload MakeSessionPayload(byte[] data, string sessionId)
            => MakePayload(PayloadType.Session, null, data, sessionId);

        public static MessagePayload MakeMessage(byte[] payload, bool encrypted, byte[] nonce = null, byte[] encryptedKey = null)
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

        public static ClientMessage MakeClientMessage(ClientMessageType type, byte[] message, CompressionType compressionType)
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

        public static ClientMessage MakeOutboundMessage(Client client, IList<string> destinations, IList<byte[]> payloads, uint maxHoldingSeconds)
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
            var signatureChainElementSerialized = EncodeSignatureChainElement(signatureChainElement);

            var signatureChain = new SignatureChain
            {
                Nonce = (uint)PseudoRandom.RandomInt(),
                SourceId = Client.AddressToId(client.address).FromHexString(),
                SourcePublicKey = client.PublicKey.FromHexString()
            };

            if (!string.IsNullOrWhiteSpace(client?.signatureChainBlockHash))
            {
                signatureChain.BlockHash = client.signatureChainBlockHash.FromHexString();
            }

            var signatures = new List<string>();

            for (int i = 0; i < destinations.Count; i++)
            {
                signatureChain.DestinationId = Client.AddressToId(destinations[i]).FromHexString();
                signatureChain.DestinationPublicKey = Client.AddressToPublicKey(destinations[i]).FromHexString();
                if (payloads.Count > 1)
                {
                    signatureChain.DataSize = (uint)payloads[i].Length;
                }
                else
                {
                    signatureChain.DataSize = (uint)payloads[0].Length;
                }

                var hex = EncodeSignatureChainMetadata(signatureChain);

                var digest = Crypto.Sha256Hex(hex);
                digest = Crypto.Sha256Hex(digest + signatureChainElementSerialized);

                var signature = client.Key.Sign(digest.FromHexString());
                signatures.Add(signature.ToHexString());
            }

            var message = new OutboundMessage
            {
                Destinations = destinations,
                Payloads = payloads,
                MaxHoldingSeconds = maxHoldingSeconds,
                Nonce = signatureChain.Nonce,
                BlockHash = signatureChain.BlockHash,
                Signatures = signatures.Select(x => x.FromHexString()).ToList()
            };

            var compressionType = payloads.Count > 1
                ? CompressionType.Zlib
                : CompressionType.None;

            var clientMessage = MakeClientMessage(ClientMessageType.OutboundMessage, message.ToBytes(), compressionType);

            return clientMessage;
        }

        public static ClientMessage MakeReceipt(CryptoKey key, string previousSignatureHex)
        {
            var signatureChainElement = new SignatureChainElement();
            var serialized = EncodeSignatureChainElement(signatureChainElement);
            var digest = Crypto.Sha256Hex(previousSignatureHex);
            digest = Crypto.Sha256Hex(digest + serialized);

            var signature = key.Sign(digest.FromHexString());

            var receipt = new Receipt
            {
                PreviousSignature = previousSignatureHex.FromHexString(),
                Signature = signature
            };

            var receiptSerialized = receipt.ToBytes();
            var message = MakeClientMessage(ClientMessageType.Receipt, receiptSerialized, CompressionType.None);

            return message;
        }

        public static string EncodeSignatureChainMetadata(SignatureChain signatureChain)
        {
            var result = "";
            result += signatureChain.Nonce.EncodeHex();
            result += signatureChain.DataSize.EncodeHex();
            result += signatureChain.BlockHash.EncodeHex();
            result += signatureChain.SourceId.EncodeHex();
            result += signatureChain.SourcePublicKey.EncodeHex();
            result += signatureChain.DestinationId.EncodeHex();
            result += signatureChain.DestinationPublicKey.EncodeHex();

            return result;
        }

        public static string EncodeSignatureChainElement(SignatureChainElement element)
        {
            var result = "";
            result += element.Id.EncodeHex();
            result += element.NextPublicKey.EncodeHex();
            result += element.IsMining.EncodeHex();

            return result;
        }
    }
}
