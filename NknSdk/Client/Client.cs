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
using System.Threading.Channels;
using NknSdk.Wallet;
using NknSdk.Common.Extensions;

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

        public IList<Action<ConnectRequest>> connectListeners = new List<Action<ConnectRequest>>();
        public IList<Func<MessageHandlerRequest, Task<object>>> messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();

        private bool shouldReconnect;
        private int reconnectInterval;
        private ClientResponseManager<string> textResponseManager;
        private ClientResponseManager<byte[]> binaryResponseManager;
        private readonly Wallet.Wallet wallet;
        private WebSocket ws;
        private GetWsAddressResult remoteNode;
        private bool isClosed;

        public Client(ClientOptions options)
        {
            this.reconnectInterval = options.ReconnectIntervalMin ?? 0;
            this.options = options;

            var key = string.IsNullOrWhiteSpace(options.Seed) 
                ? new CryptoKey() 
                : new CryptoKey(options.Seed);

            var identifier = options.Identifier ?? "";
            var address = string.IsNullOrWhiteSpace(identifier)
                ? key.PublicKey
                : identifier + "." + key.PublicKey;

            this.key = key;
            this.identifier = identifier;
            this.address = address;

            var walletOptions = new WalletOptions { Version = 1, SeedHex = key.Seed };
            this.wallet = new Wallet.Wallet(walletOptions);

            this.messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.textResponseManager = new ClientResponseManager<string>();
            this.binaryResponseManager= new ClientResponseManager<byte[]>();

            this.ConnectAsync();
        }

        public string SignatureChainBlockHash { get; private set; }

        public bool IsReady { get; private set; }

        public CryptoKey Key => this.key;

        public GetWsAddressResult RemoteNode => this.remoteNode;

        public string Seed => this.key.Seed;

        public string PublicKey => this.key.PublicKey;

        public static string AddressToId(string address) => Hash.Sha256(address);

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

            return Client.AddIdentifierPrefix(address, "__" + identifier + "__");
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

        public void OnConnect(Action<ConnectRequest> func)
        {
            this.connectListeners.Add(func);
        }

        /// <summary>
        /// Send text message to a destination address
        /// </summary>
        /// <typeparam name="T">The expected response type</typeparam>
        /// <param name="destination">Destination address to send text to</param>
        /// <param name="text">The text to send</param>
        /// <param name="options">Options</param>
        /// <returns>Response object containing result of type T</returns>
        public async Task<SendMessageResponse<T>> SendAsync<T>(
            string destination,
            string text,
            SendOptions options = null)
        {
            options ??= new SendOptions();

            try
            {
                var responseChannel = Channel.CreateBounded<T>(1);

                var messageId = await this.SendTextAsync(destination, text, options, responseChannel);

                var response = new SendMessageResponse<T> { MessageId = messageId };

                if (options.NoReply == true || messageId == null)
                {
                    return response;
                }

                var result = await responseChannel.Reader.ReadAsync().AsTask();

                response.Result = result;

                return response;
            }
            catch (Exception)
            {
                throw new ApplicationException("failed to send with any client");
            }
        }

        /// <summary>
        /// Send binary data to a destination address
        /// </summary>
        /// <typeparam name="T">The expected response type</typeparam>
        /// <param name="destination">Destination address to send data to</param>
        /// <param name="data">The data to send</param>
        /// <param name="options">Options</param>
        /// <returns>Response object containing result of type T</returns>
        public async Task<SendMessageResponse<T>> SendAsync<T>(
            string destination,
            byte[] data,
            SendOptions options = null)
        {
            options ??= new SendOptions();

            try
            {
                var responseChannel = Channel.CreateBounded<T>(1);
                var messageId = await this.SendDataAsync(destination, data, options, responseChannel);

                var response = new SendMessageResponse<T> { MessageId = messageId };

                if (options.NoReply == true || messageId == null)
                {
                    return response;
                }

                var result = await responseChannel.Reader.ReadAsync().AsTask();

                response.Result = result;

                return response;
            }
            catch (Exception)
            {
                throw new ApplicationException("failed to send with any client");
            }
        }

        internal async Task<byte[]> SendDataAsync<T>(
            IList<string> destinations, 
            byte[] data, 
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var payload = MessageFactory.MakeBinaryPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destinations, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ClientResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendDataAsync<T>(
            string destination, 
            byte[] data, 
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var payload = MessageFactory.MakeBinaryPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destination, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ClientResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendTextAsync<T>(
            string destination, 
            string text, 
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var payload = MessageFactory.MakeTextPayload(text, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destination, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ClientResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendTextAsync<T>(
            IList<string> destinations,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var payload = MessageFactory.MakeTextPayload(text, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destinations, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ClientResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendPayloadAsync(
            string destination,
            Payload payload,
            bool isEncrypted = true,
            uint maxHoldingSeconds = 0)
        {
            var dest = await this.ProcessDestinationAsync(destination);

            var message = this.MakeMessageFromPayload(payload, isEncrypted, dest);

            var serializedMessage = message.ToBytes();

            var size = serializedMessage.Length + dest.Length + Hash.SignatureLength;
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

            this.SendData(data);

            return payload.MessageId;
        }

        internal async Task<byte[]> SendPayloadAsync(
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
            var messagesBytes = this.MakeMessagesFromPayload(payload, isEncrypted, dests)
                .Select(x => x.ToBytes())
                .ToList();

            var size = 0;
            var totalSize = 0;
            var destList = new List<string>();
            var payloads = new List<byte[]>();
            var messages = new List<ClientMessage>();

            ClientMessage outboundMessage;

            for (int i = 0; i < messagesBytes.Count; i++)
            {
                size = messagesBytes[i].Length + dests[i].Length + Hash.SignatureLength;
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
                    totalSize = size;
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
                this.SendData(message.ToBytes());
            }

            return payload.MessageId;
        }

        public void Close()
        {
            this.binaryResponseManager.Stop();
            this.textResponseManager.Stop();
            this.shouldReconnect = false;

            try
            {
                if (this.ws != null)
                {
                    this.ws.Close();
                }
            }
            catch (Exception)
            {
            }

            this.isClosed = true;
        }

        internal IList<MessagePayload> MakeMessagesFromPayload(Payload payload, bool isEcrypted, IList<string> destinations)
        {
            var serializedPayload = payload.ToBytes();

            if (isEcrypted)
            {
                return this.EncryptPayload(serializedPayload, destinations);
            }

            return new List<MessagePayload> { MessageFactory.MakeMessage(serializedPayload, false) };
        }

        internal MessagePayload MakeMessageFromPayload(Payload payload, bool isEcrypted, string destination)
        {
            var serializedPayload = payload.ToBytes();

            if (isEcrypted)
            {
                return this.EncryptPayload(serializedPayload, destination);
            }

            return MessageFactory.MakeMessage(serializedPayload, false);
        }

        internal async Task<IList<string>> ProcessDestinationsAsync(IList<string> destinations)
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

        internal async Task<string> ProcessDestinationAsync(string destination)
        {
            if (destination.Length == 0)
            {
                throw new InvalidDestinationException("destination is empty");
            }

            var destinationParts = destination.Split('.');
            if (destinationParts[destinationParts.Length - 1].Length < Hash.PublicKeyLength * 2)
            {
                var registrantResponse = await this.GetRegistrantAsync(destinationParts[destinationParts.Length - 1]);
                if (registrantResponse.Registrant != null && registrantResponse.Registrant.Length > 0)
                {
                    destinationParts[destinationParts.Length - 1] = registrantResponse.Registrant;
                }
                else
                {
                    throw new InvalidDestinationException(destination + " is neither a valid public key nor a registered name");
                }
            }

            return string.Join(".", destinationParts);
        }

        internal void SendAck(IEnumerable<string> destinations, byte[] messageId, bool isEncrypted)
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

        internal void SendAck(string destination, byte[] messageId, bool isEncrypted)
        {
            var payload = MessageFactory.MakeAckPayload(messageId.ToHexString(), null);
            var messagePayload = this.MakeMessageFromPayload(payload, isEncrypted, destination);
            var message = MessageFactory.MakeOutboundMessage(
                this, 
                new string[] { destination }, 
                new List<byte[]> { ProtoSerializer.Serialize(messagePayload) }, 
                0);

            var messageBytes = message.ToBytes();

            this.SendData(messageBytes);
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

        private void SendData(byte[] data)
        {
            if (this.ws == null)
            {
                throw new ClientNotReadyException();
            }

            this.ws.Send(data);
        }

        private void AddResponseProcessor<T>(ClientResponseProcessor<T> responseProcessor)
        {
            var typeofResponse = typeof(T);
            if (typeofResponse == typeof(string))
            {
                this.textResponseManager.Add(responseProcessor as ClientResponseProcessor<string>);
            }
            else if (typeofResponse == typeof(byte[]))
            {
                this.binaryResponseManager.Add(responseProcessor as ClientResponseProcessor<byte[]>);
            }

            throw new InvalidArgumentException(nameof(responseProcessor));
        }

        private IList<MessagePayload> EncryptPayload(byte[] payload, IList<string> destinations)
        {
            var nonce = PseudoRandom.RandomBytes(Hash.NonceLength);
            var key = PseudoRandom.RandomBytes(Hash.KeyLength);
            var encryptedPayload = Hash.EncryptSymmetric(payload, nonce, key);

            var result = new List<MessagePayload>();
            for (int i = 0; i < destinations.Count; i++)
            {
                var publicKey = Client.AddressToPublicKey(destinations[i]);
                var encryptedKey = this.Key.Encrypt(key, publicKey);

                var mergedNonce = encryptedKey.Nonce.Concat(nonce).ToArray();

                var message = MessageFactory.MakeMessage(encryptedPayload, true, mergedNonce, encryptedKey.Message);

                result.Add(message);
            }

            return result;
        }

        private MessagePayload EncryptPayload(byte[] payload, string destination)
        {
            var publicKey = Client.AddressToPublicKey(destination);
            var encryptedKey = this.key.Encrypt(payload, publicKey);

            var message = MessageFactory.MakeMessage(encryptedKey.Message, true, encryptedKey.Nonce);

            return message;
        }

        private async Task<bool> HandleInboundMessageAsync(byte[] data)
        {
            var inboundMessage = data.FromBytes<InboundMessage>();
            if (inboundMessage.PreviousSignature?.Length > 0)
            {
                var previousSignatureHex = inboundMessage.PreviousSignature.ToHexString();

                var receipt = MessageFactory.MakeReceipt(this.key, previousSignatureHex);
                var receiptBytes = receipt.ToBytes();

                this.SendData(receiptBytes);
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
                    this.textResponseManager.Respond(payload.ReplyToId, null, payload.Type);
                    this.binaryResponseManager.Respond(payload.ReplyToId, null, payload.Type);
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
                    this.binaryResponseManager.Respond(payload.ReplyToId, payload.Data, payload.Type);
                }
                else
                {
                    this.textResponseManager.Respond(payload.ReplyToId, textMessage, payload.Type);
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
                            return await func(request);
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
                            await this.SendDataAsync<byte[]>(inboundMessage.Source, bytes, new SendOptions
                            {
                                IsEncrypted = messagePayload.IsEncrypted,
                                ReplyToId = payload.MessageId.ToHexString()
                            });

                            responded = true;
                            break;
                        }
                        else if (response is string text)
                        {
                            await this.SendTextAsync<string>(inboundMessage.Source, text, new SendOptions
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

            if (this.ws != null)
            {
                this.ws.OnClose -= OnWebSocketClosed;

                try
                {
                    this.ws.Close();
                }
                catch (Exception)
                {
                }
            }

            this.ws = ws;
            this.remoteNode = remoteNode;

            this.ws.OnOpen += (object sender, EventArgs e) =>
            {
                var message = JsonSerializer.ToJsonString(new { Action = "setClient", Addr = this.address });
                this.ws.Send(message);

                this.shouldReconnect = true;
                this.reconnectInterval = this.options.ReconnectIntervalMin ?? 0;
            };

            this.ws.OnMessage += (object sender, MessageEventArgs e) =>
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
                        this.SignatureChainBlockHash = setClientMessage.Result.SigChainBlockHash;

                        if (this.IsReady == false)
                        {
                            this.IsReady = true;
                            Console.WriteLine("***Client connected***");

                            if (this.connectListeners.Count > 0)
                            {
                                foreach (var listener in this.connectListeners)
                                {
                                    listener(new ConnectRequest { Address = setClientMessage.Result.Node.Address });
                                }
                            }

                            this.Connected?.Invoke(this, new EventArgs());
                        }

                        break;

                    case "updateSigChainBlockHash":
                        var signatureBlockHashMessage = JsonSerializer.Deserialize<UpdateSigChainBlockHashMessage>(e.Data);
                        this.SignatureChainBlockHash = signatureBlockHashMessage.Result;
                        break;

                    default: break;
                }
            };

            this.ws.OnClose += OnWebSocketClosed;

            this.ws.OnError += (object sender, ErrorEventArgs e) =>
            {
                Console.WriteLine("[Socket Error] " + e.Message);
            };

            this.ws.Connect();
        }

        private byte[] GetPayload(MessagePayload message, string source)
            => message.IsEncrypted
                ? this.DecryptPayload(message, source)
                : message.Payload;

        private byte[] DecryptPayload(MessagePayload message, string sourceAddress)
        {
            var rawPayload = message.Payload;
            var sourcePublicKey = Client.AddressToPublicKey(sourceAddress);
            var nonce = message.Nonce;
            var encryptedKey = message.EncryptedKey;

            byte[] decryptedPayload = null;
            if (encryptedKey != null && encryptedKey.Length > 0)
            {
                if (nonce.Length != Hash.NonceLength * 2)
                {
                    throw new DecryptionException("invalid nonce length");
                }

                var sharedKey = this.key.Decrypt(
                    encryptedKey, 
                    nonce.Take(Hash.NonceLength).ToArray(), 
                    sourcePublicKey);

                if (sharedKey == null)
                {
                    throw new DecryptionException("decrypt shared key failed");
                }

                decryptedPayload = Hash.DecryptSymmetric(rawPayload, nonce.Skip(Hash.NonceLength).ToArray(), sharedKey);
                if (decryptedPayload == null)
                {
                    throw new DecryptionException("decrypt message failed");
                }
            }
            else 
            {
                if (nonce.Length != Hash.NonceLength)
                {
                    throw new DecryptionException("invalid nonce length");
                }

                decryptedPayload = this.key.Decrypt(rawPayload, nonce, sourcePublicKey);
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
