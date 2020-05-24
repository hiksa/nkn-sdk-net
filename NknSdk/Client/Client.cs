using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

//using WebSocket4Net;
using Utf8Json;
using WebSocketSharp;

using NknSdk.Client.Model;
using NknSdk.Common;
using NknSdk.Common.Protobuf.Payloads;
using NknSdk.Common.Rpc;
using NknSdk.Common.Rpc.Results;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;

using static NknSdk.Client.Handlers;
using System.ComponentModel.Design.Serialization;
using System.Security.Cryptography;
using Microsoft.VisualBasic;
using NknSdk.Common.Exceptions;

namespace NknSdk.Client
{
    public class Client
    {
        private ClientOptions options;
        private readonly CryptoKey key;
        private string identifier;
        public string address;

        public event EventHandler Connected;
        public event TextMessageHandler TextReceived;
        public event DataMessageHandler DataReceived;

        public IList<Action> connectListeners = new List<Action>();
        public IList<Func<MessageHandlerRequest, Task<object>>> messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();

        public string signatureChainBlockHash;
        private bool shouldReconnect;
        private int reconnectInterval;
        private ClientResponseManager responseManager;
        private WebSocket webSocket;
        private GetWsAddressResult remoteNode;
        private bool isClosed;

        public Client(ClientOptions options)
        {
            var key = string.IsNullOrWhiteSpace(options.Seed) 
                ? new CryptoKey() 
                : new CryptoKey(options.Seed);

            var identifier = options.Identifier ?? "";
            var address = string.IsNullOrWhiteSpace(identifier)
                ? key.PublicKey
                : identifier + "." + key.PublicKey;

            this.options = options;

            this.key = key;
            this.identifier = identifier;
            this.messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.address = address;
            this.reconnectInterval = options.ReconnectIntervalMin ?? 0;
            this.responseManager = new ClientResponseManager();

            this.ConnectAsync();
        }

        public bool IsReady { get; private set; }

        public CryptoKey Key => this.key;

        public GetWsAddressResult RemoteNode => this.remoteNode;

        public string Seed => this.key.Seed;

        public string PublicKey => this.key.PublicKey;

        public static string AddressToId(string address) => Crypto.Sha256(address);

        public static string AddressToPublicKey(string address)
            => address
                .Split(new char[] { '.' })
                .LastOrDefault();

        public static string AddIdentifier(string address, string identifier)
        {
            if (identifier == "")
            {
                return address;
            }

            return AddIdentifierPrefix(address, "__" + identifier + "__");
        }

        public static string AddIdentifierPrefix(string identifier, string prefix)
        {
            if (identifier == "")
            {
                return "" + prefix;
            }

            if (prefix == "")
            {
                return "" + identifier;
            }

            return prefix + "." + identifier;
        }

        public static (string Address, string ClientId) RemoveIdentifier(string source)
        {
            var parts = source.Split('.');
            if (Constants.MultiClientIdentifierRegex.IsMatch(parts[0]))
            {
                var address = string.Join(".", parts.Skip(1));
                return (address, parts[0]);
            }

            return (source, "");
        }

        public void OnMessage(Func<MessageHandlerRequest, Task<object>> func) => this.messageListeners.Add(func);

        public async Task<byte[]> SendDataAsync(
            IList<string> destinations, 
            byte[] data, 
            SendOptions options,
            ResponseHandler responseCallback = null,
            TimeoutHandler timeoutCallback = null)
        {
            var payload = MessageFactory.MakeBinaryPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destinations, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseCallback != null)
            {
                this.responseManager.Add(new ClientResponseProcessor(messageId, options.ResponseTimeout, responseCallback, timeoutCallback));
            }

            return messageId;
        }

        public async Task<byte[]> SendTextAsync(
            IList<string> destinations, 
            string text, 
            SendOptions options,
            ResponseHandler responseCallback = null,
            TimeoutHandler timeoutCallback = null)
        {
            var payload = MessageFactory.MakeTextPayload(text, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destinations, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseCallback != null)
            {
                this.responseManager.Add(new ClientResponseProcessor(messageId, options.ResponseTimeout, responseCallback, timeoutCallback));
            }

            return messageId;
        }

        public async Task<byte[]> SendDataAsync(
            string destination, 
            byte[] data, 
            SendOptions options,
            ResponseHandler responseCallback = null, 
            TimeoutHandler timeoutCallback = null)
        {
            var payload = MessageFactory.MakeBinaryPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destination, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseCallback != null)
            {
                var responseProcessor = new ClientResponseProcessor(messageId, options.ResponseTimeout, responseCallback, timeoutCallback);
                this.responseManager.Add(responseProcessor);
            }

            return messageId;
        }

        public async Task<byte[]> SendTextAsync(
            string destination, 
            string data, 
            SendOptions options,
            ResponseHandler responseCallback = null,
            TimeoutHandler timeoutCallback = null)
        {
            var payload = MessageFactory.MakeTextPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destination, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseCallback != null)
            {
                this.responseManager.Add(new ClientResponseProcessor(messageId, options.ResponseTimeout, responseCallback, timeoutCallback));
            }

            return messageId;
        }

        public async Task<byte[]> SendPayloadAsync(
            string destination,
            Payload payload,
            bool isEncrypted = true,
            uint maxHoldingSeconds = 0)
        {
            var dest = await this.ProcessDestinationAsync(destination);

            var message = this.MakeMessageFromPayload(payload, isEncrypted, dest);

            var serializedMessage = message.ToBytes();

            var size = serializedMessage.Length + dest.Length + Crypto.SignatureLength;
            if (size > Constants.MaxClientMessageSize)
            {
                throw new DataSizeTooLargeException($"encoded message is greater than {Constants.MaxClientMessageSize} bytes");
            }

            var outboundMessage = MessageFactory.MakeOutboundMessage(
                this,
                new List<string> { dest },
                new byte[][] { serializedMessage },
                maxHoldingSeconds);

            var data = outboundMessage.ToBytes();

            this.WsSend(data);

            return payload.MessageId;
        }

        public async Task<byte[]> SendPayloadAsync(
            IList<string> destinations, 
            Payload payload, 
            bool isEncrypted = true, 
            uint maxHoldingSeconds = 0)
        {
            if (destinations.Count == 0)
            {
                return null;
            }
            else if (destinations.Count == 1)
            {
                return await this.SendPayloadAsync(destinations[0], payload, isEncrypted, maxHoldingSeconds);
            }

            var dests = await this.ProcessDestinationsAsync(destinations);
            var messagesBytes = dests.Select(d => this.MakeMessageFromPayload(payload, isEncrypted, d).ToBytes()).ToList();

            var size = 0;
            var totalSize = 0;
            var destList = new List<string>();
            var payloads = new List<byte[]>();
            var messages = new List<ClientMessage>();
            ClientMessage outboundMessage;

            for (int i = 0; i < messagesBytes.Count; i++)
            {
                size = messagesBytes[i].Length + dests[i].Length + Crypto.SignatureLength;
                if (size > Constants.MaxClientMessageSize)
                {
                    throw new DataSizeTooLargeException($"encoded message is greater than {Constants.MaxClientMessageSize} bytes");
                }

                if (totalSize + size > Constants.MaxClientMessageSize)
                {
                    outboundMessage = MessageFactory.MakeOutboundMessage(
                        this,
                        destList,
                        payloads,
                        maxHoldingSeconds);

                    messages.Add(outboundMessage);

                    destList.Clear();
                    payloads.Clear();
                    totalSize += size;
                }

                destList.Add(dests[i]);
                payloads.Add(messagesBytes[i]);
                totalSize += size;
            }

            outboundMessage = MessageFactory.MakeOutboundMessage(
                this,
                destList,
                payloads,
                maxHoldingSeconds);

            messages.Add(outboundMessage);

            foreach (var message in messages)
            {
                this.WsSend(message.ToBytes());
            }

            return payload.MessageId;
        }

        public void Close()
        {
            this.responseManager.Stop();
            this.shouldReconnect = false;

            try
            {
                if (this.webSocket != null)
                {
                    this.webSocket.Close();
                }
            }
            catch (Exception)
            {
            }

            this.isClosed = true;
        }

        private async Task ConnectAsync()
        {
            var useTls = this.ShouldUseTls();
            for (int i = 0; i < 3; i++)
            {
                GetWsAddressResult getWsAddressResult;
                try
                {
                    if (useTls)
                    {
                        getWsAddressResult = await RpcClient.GetWssAddress(this.options.RpcServerAddress, this.address);
                    }
                    else
                    {
                        getWsAddressResult = await RpcClient.GetWsAddress(this.options.RpcServerAddress, this.address);
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                await this.OnNewWsAddressAsync(getWsAddressResult);

                return;
            }

            if (this.shouldReconnect)
            {
                await this.ReconnectAsync();
            }
        }

        private void WsSend(byte[] data)
        {
            if (this.webSocket == null)
            {
                throw new ClientNotReadyException();
            }

            this.webSocket.Send(data);
        }

        private MessagePayload EncryptPayload(byte[] payload, string dest)
        {
            var publicKey = Client.AddressToPublicKey(dest);
            var encrypted = this.key.Encrypt(payload, publicKey);
            var message = MessageFactory.MakeMessage(encrypted.Message, true, encrypted.Nonce);
            return message;
        }

        public MessagePayload MakeMessageFromPayload(Payload payload, bool isEcrypted, string dest)
        {
            var serializedPayload = payload.ToBytes();
            if (isEcrypted)
            {
                return this.EncryptPayload(serializedPayload, dest);
            }

            return MessageFactory.MakeMessage(serializedPayload, false);
        }

        public async Task<IList<string>> ProcessDestinationsAsync(IList<string> destinations)
        {
            if (destinations.Count == 0)
            {
                throw new InvalidDestinationException("no destinations");
            }

            var tasks = destinations.Select(this.ProcessDestinationAsync).ToArray();

            var result = (await Task.WhenAll(tasks)).Where(x => x.Length > 0).ToList();

            if (result.Count == 0)
            {
                throw new InvalidDestinationException("all destinations are invalid");
            }

            return result;
        }

        public async Task<string> ProcessDestinationAsync(string destination)
        {
            if (destination.Length == 0)
            {
                throw new InvalidDestinationException("destination is empty");
            }

            var destinationParts = destination.Split('.');
            if (destinationParts[destinationParts.Length - 1].Length < Crypto.PublicKeyLength * 2)
            {
                var response = await this.GetRegistrantAsync(destinationParts[destinationParts.Length - 1]);
                if (response.Registrant != null && response.Registrant.Length > 0)
                {
                    destinationParts[destinationParts.Length - 1] = response.Registrant;
                }
                else
                {
                    throw new InvalidDestinationException(destination + " is neither a valid public key nor a registered name");
                }
            }

            return string.Join(".", destinationParts);
        }

        private async Task<GetRegistrantResult> GetRegistrantAsync(string name)
        {
            return await RpcClient.GetRegistrant(this.options.RpcServerAddress, name);
        }

        private async Task OnNewWsAddressAsync(GetWsAddressResult remoteNode)
        {
            if (string.IsNullOrWhiteSpace(remoteNode.Address))
            {
                if (this.shouldReconnect)
                {
                    await this.ReconnectAsync();
                }

                return;
            }

            var ws = default(WebSocket);
            var protocol = this.ShouldUseTls() ? "wss://" : "ws://";
            var uri = $"{protocol}{remoteNode.Address}";

            try
            {
                ws = new WebSocket(uri);
            }
            catch (Exception)
            {
                if (this.shouldReconnect)
                {
                    await this.ReconnectAsync();
                }

                return;
            }

            if (this.webSocket != null)
            {
                this.webSocket.OnClose -= OnWebSocketClosed;

                try
                {
                    this.webSocket.Close();
                }
                catch (Exception)
                {
                }
            }

            this.webSocket = ws;
            this.remoteNode = remoteNode;

            this.webSocket.OnOpen += (object sender, EventArgs e) =>
            {
                var message = JsonSerializer.ToJsonString(new { Action = "setClient", Addr = this.address });
                this.webSocket.Send(message);

                this.shouldReconnect = true;
                this.reconnectInterval = this.options.ReconnectIntervalMin ?? 0;
            };

            this.webSocket.OnMessage += (object sender, MessageEventArgs e) =>
            {
                if (e.Data[0] != '{')
                {
                    var clientMessage = e.RawData.FromBytes<ClientMessage>();
                    switch (clientMessage.Type)
                    {
                        case ClientMessageType.InboundMessage:
                            this.HandleInboundMessageAsync(clientMessage.Message).GetAwaiter().GetResult();
                            break;
                        default:
                            break;
                    }

                    return;
                }

                var message = JsonSerializer.Deserialize<Message>(e.Data);

                if (message.HasError)
                {
                    Console.WriteLine(message.Error);
                    return;
                }

                switch (message.Action)
                {
                    case "setClient":                        
                        var setClientMessage = JsonSerializer.Deserialize<SetClientMessage>(e.Data);
                        this.signatureChainBlockHash = setClientMessage.Result.SigChainBlockHash;
                        if (this.IsReady == false)
                        {
                            this.IsReady = true;
                            Console.WriteLine("Client connected");
                            this.Connected?.Invoke(this, new EventArgs());
                        }

                        break;

                    case "updateSigChainBlockHash":
                        var signatureBlockHashMessage = JsonSerializer.Deserialize<UpdateSigChainBlockHashMessage>(e.Data);
                        this.signatureChainBlockHash = signatureBlockHashMessage.Result;
                        break;

                    default: break;
                }
            };

            //this.webSocket.DataReceived += (object sender, DataReceivedEventArgs e) =>
            //{
            //    var clientMessage = e.Data.FromBytes<ClientMessage>();
            //    switch (clientMessage.Type)
            //    {
            //        case ClientMessageType.InboundMessage:
            //            this.HandleInboundMessageAsync(clientMessage.Message).GetAwaiter().GetResult();
            //            break;
            //        default:
            //            break;
            //    }
            //};

            this.webSocket.OnClose += OnWebSocketClosed;

            this.webSocket.OnError += (object sender, ErrorEventArgs e) =>
            {
                //Console.WriteLine("[Socket Error] " + e.Exception.Message);
                Console.WriteLine("[Socket Error] " + e.Message);
            };

            this.webSocket.Connect();
        }

        private async Task<bool> HandleInboundMessageAsync(byte[] data)
        {
            var inboundMessage = data.FromBytes<InboundMessage>();
            if (inboundMessage.PreviousSignature?.Length > 0)
            {
                var previousSignatureHex = inboundMessage.PreviousSignature.ToHexString();
                var receipt = MessageFactory.MakeReceipt(this.key, previousSignatureHex);
                var receiptBytes = receipt.ToBytes();

                this.WsSend(receiptBytes);
            }

            var messagePayload = ProtoSerializer.Deserialize<MessagePayload>(inboundMessage.Payload);

            var payloadBytes = this.GetPayload(messagePayload, inboundMessage.Source);

            var payload = ProtoSerializer.Deserialize<Payload>(payloadBytes);

            string textMessage = null;

            switch (payload.Type)
            {
                case PayloadType.Binary:
                    break;
                case PayloadType.Text:
                    var textData = ProtoSerializer.Deserialize<TextDataPayload>(payload.Data);
                    textMessage = textData.Text;
                    break;
                case PayloadType.Ack:
                    this.responseManager.Respond(payload.ReplyToId, null, payload.Type);
                    return true;
                case PayloadType.Session:
                    break;
                default:
                    break;
            }

            if (payload.ReplyToId?.Length > 0)
            {
                if (textMessage == null)
                {
                    this.responseManager.Respond(payload.ReplyToId, payload.Data, payload.Type);
                }
                else
                {
                    this.responseManager.Respond(payload.ReplyToId, textMessage, payload.Type);
                }

                return true;
            }

            var responses = Enumerable.Empty<object>();
            switch (payload.Type)
            {
                case PayloadType.Session:
                case PayloadType.Binary:
                    this.DataReceived?.Invoke(inboundMessage.Source, payload.Data);

                    var request = new MessageHandlerRequest
                    {
                        Source = inboundMessage.Source,
                        Payload = payload.Data,
                        PayloadType = payload.Type,
                        IsEncrypted = messagePayload.IsEncrypted,
                        MessageId = payload.MessageId,
                        NoReply = payload.NoReply
                    };

                    var tasks = this.messageListeners.Select(async func =>
                    {
                        try
                        {
                            var result = await func(request);

                            return result;
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    });

                    responses = await Task.WhenAll(tasks);

                    break;
                case PayloadType.Text:
                    this.TextReceived?.Invoke(inboundMessage.Source, textMessage);
                    break;
                case PayloadType.Ack: 
                    break;
            }

            if (payload.NoReply == false)
            {
                var responded = false;
                foreach (var response in responses)
                {
                    if (response is bool res)
                    {
                        if (res == false)
                        {
                            return true;
                        }
                    }
                    else if (response != null)
                    {
                        if (response is byte[] bytes)
                        {
                            await this.SendDataAsync(inboundMessage.Source, bytes, new SendOptions
                            {
                                IsEncrypted = messagePayload.IsEncrypted,
                                ReplyToId = payload.MessageId.ToHexString()
                            });

                            responded = true;
                            break;

                        }
                        else if (response is string text)
                        {
                            await this.SendTextAsync(inboundMessage.Source, text, new SendOptions
                            {
                                IsEncrypted = messagePayload.IsEncrypted,
                                ReplyToId = payload.MessageId.ToHexString()
                            });

                            responded = true;
                            break;
                        }
                    }
                }

                if (responded == false)
                {
                    this.SendAck(inboundMessage.Source, payload.MessageId, messagePayload.IsEncrypted);
                }
            }

            return true;
        }

        public void SendAck(IEnumerable<string> destinations, byte[] messageId, bool isEncrypted)
        {
            if (destinations.Count() == 1)
            {
                this.SendAck(destinations.First(), messageId, isEncrypted);
            }
            else if (destinations.Count() > 1 && isEncrypted)
            {
                foreach (var destination in destinations)
                {
                    this.SendAck(destination, messageId, isEncrypted);
                }
            }
        }

        public void SendAck(string destination, byte[] messageId, bool isEncrypted)
        {
            var payload = MessageFactory.MakeAckPayload(messageId.ToHexString(), null);
            var messagePayload = this.MakeMessageFromPayload(payload, isEncrypted, destination);
            var message = MessageFactory.MakeOutboundMessage(
                this, 
                new string[] { destination }, 
                new List<byte[]> { ProtoSerializer.Serialize(messagePayload) }, 
                0);

            this.WsSend(ProtoSerializer.Serialize(message));
        }

        private byte[] GetPayload(MessagePayload message, string source)
            => message.IsEncrypted
                ? this.DecryptPayload(message, source)
                : message.Payload;

        private byte[] DecryptPayload(MessagePayload message, string source)
        {
            var rawPayload = message.Payload;
            var sourcePublicKey = Client.AddressToPublicKey(source);
            var nonce = message.Nonce;
            var encryptedKey = message.EncryptedKey;

            byte[] decryptedPayload = null;
            if (encryptedKey != null && encryptedKey.Length > 0)
            {
                if (nonce.Length != Crypto.NonceLength * 2)
                {
                    throw new DecryptionException("invalid nonce length");
                }

                var sharedKey = this.key.Decrypt(encryptedKey, nonce.Take(Crypto.NonceLength).ToArray(), sourcePublicKey);
                if (sharedKey == null)
                {
                    throw new DecryptionException("decrypt shared key failed");
                }

                decryptedPayload = Crypto.DecryptSymmetric(rawPayload, nonce.Skip(Crypto.NonceLength).ToArray(), sharedKey);
                if (decryptedPayload == null)
                {
                    throw new DecryptionException("decrypt message failed");
                }
            }
            else 
            {
                if (nonce.Length != Crypto.NonceLength)
                {
                    throw new DecryptionException("invalid nonce length");
                }

                decryptedPayload = this.Key.Decrypt(rawPayload, nonce, sourcePublicKey);
                if (decryptedPayload == null)
                {
                    throw new DecryptionException("decrypt message failed");
                }
            }

            return decryptedPayload;
        }

        private void OnWebSocketClosed(object sender, EventArgs e)
        {
            Console.WriteLine("Socket closed.");
            if (this.shouldReconnect)
            {
                this.ReconnectAsync().GetAwaiter().GetResult();
            }
        }

        private async Task ReconnectAsync()
        {
            await Task.Factory.StartNew(async () =>
            {
                Thread.Sleep(this.reconnectInterval);
                await this.ConnectAsync();
            });

            this.reconnectInterval *= 2;
            if (this.reconnectInterval > this.options.ReconnectIntervalMax)
            {
                this.reconnectInterval = this.options.ReconnectIntervalMax.Value;
            }
        }

        private bool ShouldUseTls()
            => this.options.UseTls.HasValue && this.options.UseTls == true;
    }
}
