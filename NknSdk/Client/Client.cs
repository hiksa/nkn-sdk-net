using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Utf8Json;
using WebSocketSharp;

using Ncp;

using NknSdk.Client.Messages;
using NknSdk.Client.Requests;
using NknSdk.Common;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Extensions;
using NknSdk.Wallet.Models;
using NknSdk.Common.Options;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Messages;
using NknSdk.Common.Protobuf.Payloads;
using NknSdk.Common.Rpc;
using NknSdk.Common.Rpc.Results;

using Constants = NknSdk.Common.Constants;

namespace NknSdk.Client
{
    public class Client : ITransactionSender
    {
        private static readonly Type byteArrayType = typeof(byte[]);
        private static readonly Type stringType = typeof(string);

        private readonly CryptoKey key;

        private bool isClosed;
        private string identifier;
        private int reconnectInterval;
        private bool shouldReconnect;
        private WebSocket ws;
        private ClientOptions options;
        private ResponseManager<string> textResponseManager;
        private ResponseManager<byte[]> binaryResponseManager;

        internal IList<Action<ConnectHandlerRequest>> connectHandlers = new List<Action<ConnectHandlerRequest>>();
        internal IList<Func<MessageHandlerRequest, Task<object>>> messageHandlers = new List<Func<MessageHandlerRequest, Task<object>>>();

        public Client(ClientOptions options)
        {
            this.reconnectInterval = options.ReconnectIntervalMin ?? 0;
            this.options = options;

            var key = string.IsNullOrWhiteSpace(options.SeedHex)
                ? new CryptoKey()
                : new CryptoKey(options.SeedHex);

            var identifier = options.Identifier ?? "";
            var address = string.IsNullOrWhiteSpace(identifier)
                ? key.PublicKey
                : identifier + "." + key.PublicKey;

            this.key = key;
            this.identifier = identifier;
            this.Address = address;

            var walletOptions = new WalletOptions { Version = 1, SeedHex = key.SeedHex };
            this.Wallet = new Wallet.Wallet(walletOptions);

            this.options.SeedHex = null;

            this.messageHandlers = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.textResponseManager = new ResponseManager<string>();
            this.binaryResponseManager = new ResponseManager<byte[]>();

            Task.Run(this.ConnectAsync);
        }

        public string Address { get; }

        public Wallet.Wallet Wallet { get; }

        public string SignatureChainBlockHash { get; private set; }

        public bool IsReady { get; private set; }

        public CryptoKey Key => this.key;

        /// <summary>
        /// Gets the client's secret seed as hex string value 
        /// </summary>
        public string SeedHex => this.key.SeedHex;

        /// <summary>
        /// Gets the client's public key as hex string value 
        /// </summary>
        public string PublicKey => this.key.PublicKey;

        public GetWsAddressResult RemoteNode { get; private set; }

        private bool IsUsingTls => this.options.UseTls.HasValue && this.options.UseTls == true;

        /// <summary>
        /// Adds a callback method that will be executed when the client accepts a new message. 
        /// Multiple callbacks will be called sequentially in order of added. Can be async method in which case
        /// call will wait for previous task to comple before calling the next. 
        /// If the first non-null returned value is byte[] or string the value will be sent back as reply; 
        /// if the first non-null returned value is false, no reply or ACK will be sent;
        /// if all handler callbacks return null or are void, an ACK indicating message was received will be sent back. 
        /// Receiving reply or ACK will not trigger this callback.
        /// </summary>
        /// <param name="func">The callback method</param>
        public void OnMessage(Func<MessageHandlerRequest, Task<object>> func) => this.messageHandlers.Add(func);

        /// <summary>
        /// Adds a callback method that will be executed when the client connects to a node.
        /// Multiple listeners will be called sequentially in the order of being added.
        /// Note that callbacks added after the client is connected to a node for a first time
        /// (i.e. 'client.isReady = true') will not be called.
        /// </summary>
        /// <param name="func">The callback method</param>
        public void OnConnect(Action<ConnectHandlerRequest> func) => this.connectHandlers.Add(func);

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

        /// <summary>
        /// Send text message to a destination address. Returns a message response object,
        /// containing result of type TResponse. This type can either be string or byte[].
        /// A null value will be returned if the destination sent no data along with the reponse.
        /// An error message will be populated on the response object for any potential failures
        /// </summary>
        /// <typeparam name="TResponse">The expected response type can be either string or byte[]</typeparam>
        /// <param name="destination">Destination address to send text to</param>
        /// <param name="text">The text to send</param>
        /// <param name="options">Options</param>
        /// <returns>
        /// Response object containing result of type TResponse (string or byte[]), 
        /// or an error message indicating any potential failures error message
        /// </returns>
        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            string destination,
            string text,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTask = this.SendTextAsync(destination, text, options, responseChannel, timeoutChannel);

                return await HandleSendResultTaskAsync(resultTask, options, responseChannel, timeoutChannel);
            }
            catch (Exception)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send message" };
            }
        }

        /// <summary>
        /// Send binary data to a destination address. Returns a message response object,
        /// containing result of type TResponse. This type can either be string or byte[].
        /// A null value will be returned if the destination sent no data along with the reponse.
        /// An error message will be populated on the response object for any potential failures
        /// </summary>
        /// <typeparam name="TResponse">The expected response type can be either string or byte[]</typeparam>
        /// <param name="destination">Destination address to send text to</param>
        /// <param name="text">The text to send</param>
        /// <param name="options">Options</param>
        /// <returns>
        /// Response object containing result of type TResponse (string or byte[]), 
        /// or an error message indicating any potential failures error message
        /// </returns>
        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            string destination,
            byte[] data,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTask = this.SendDataAsync(destination, data, options, responseChannel);

                return await HandleSendResultTaskAsync(resultTask, options, responseChannel, timeoutChannel);
            }
            catch (Exception)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send message" };
            }
        }

        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            IEnumerable<string> destinations,
            byte[] data,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTask = this.SendDataManyAsync<byte[]>(destinations, data, options);

                return await HandleSendResultTaskAsync(resultTask, options, responseChannel, timeoutChannel);
            }
            catch (Exception)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send message" };
            }
        }

        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            IEnumerable<string> destinations,
            string text,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTask = this.SendTextManyAsync<byte[]>(destinations, text, options);

                return await HandleSendResultTaskAsync(resultTask, options, responseChannel, timeoutChannel);
            }
            catch (Exception)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send message" };
            }
        }

        public async Task<SendMessageResponse<byte[]>> PublishAsync(
            string topic,
            string text,
            PublishOptions options = null)
        {
            options = options ?? new PublishOptions();
            options.NoReply = true;

            var subscribers = await this.GetAllSubscribersAsync(topic, options);

            return await this.SendAsync<byte[]>(subscribers.ToList(), text, SendOptions.NewFrom(options));
        }

        public async Task<SendMessageResponse<byte[]>> PublishAsync(
            string topic,
            byte[] data,
            PublishOptions options = null)
        {
            options = options ?? new PublishOptions();
            options.NoReply = true;

            var subscribers = await this.GetAllSubscribersAsync(topic, options);

            return await this.SendAsync<byte[]>(subscribers.ToList(), data, SendOptions.NewFrom(options));
        }

        public async Task<GetLatestBlockHashResult> GetLatestBlockAsync()
        {
            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {
                    return await NknSdk.Wallet.Wallet.GetLatestBlockAsync(this.Wallet.Options);
                }
                catch (Exception)
                {
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options);
            return await NknSdk.Wallet.Wallet.GetLatestBlockAsync(walletOptions);
        }

        public async Task<GetRegistrantResult> GetRegistrantAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {
                    return await NknSdk.Wallet.Wallet.GetRegistrantAsync(name, this.Wallet.Options);
                }
                catch (Exception)
                {
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options);
            return await NknSdk.Wallet.Wallet.GetRegistrantAsync(name, walletOptions);
        }

        public async Task<GetSubscribersWithMetadataResult> GetSubscribersWithMetadataAsync(
            string topic, 
            PublishOptions options = null)
        {
            options = options ?? new PublishOptions();

            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {
                    var mergedOptions = this.Wallet.Options.MergeWith(options);
                    return await NknSdk.Wallet.Wallet.GetSubscribersWithMetadataAsync(topic, mergedOptions);
                }
                catch (Exception)
                {
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options).AssignFrom(options);
            return await NknSdk.Wallet.Wallet.GetSubscribersWithMetadataAsync(topic, walletOptions);
        }

        public async Task<GetSubscribersResult> GetSubscribersAsync(string topic, PublishOptions options = null)
        {
            options = options ?? new PublishOptions();

            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {
                    var mergedOptions = this.Wallet.Options.MergeWith(options);
                    return await NknSdk.Wallet.Wallet.GetSubscribersAsync(topic, mergedOptions);
                }
                catch (Exception)
                {
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options).AssignFrom(options);
            return await NknSdk.Wallet.Wallet.GetSubscribersAsync(topic, walletOptions);
        }

        public async Task<long> GetSubscrinersCountAsync(string topic)
        {
            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress) == false)
            {
                try
                {
                    return await NknSdk.Wallet.Wallet.GetSubscribersCountAsync(topic, this.Wallet.Options);
                }
                catch (Exception)
                {
                }
            }

            return await NknSdk.Wallet.Wallet.GetSubscribersCountAsync(topic, WalletOptions.NewFrom(this.options));
        }

        public async Task<GetSubscriptionResult> GetSubscriptionAsync(string topic, string subscriber)
        {
            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress) == false)
            {
                try
                {
                    return await NknSdk.Wallet.Wallet.GetSubscriptionAsync(topic, subscriber, this.Wallet.Options);
                }
                catch (Exception)
                {
                }
            }

            return await NknSdk.Wallet.Wallet.GetSubscriptionAsync(topic, subscriber, WalletOptions.NewFrom(this.options));
        }

        public async Task<Amount> GetBalanceAsync(string address = "", WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            var addr = string.IsNullOrEmpty(address) ? this.Wallet.Address : address;

            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {

                    var balanceResult = await NknSdk.Wallet.Wallet.GetBalanceAsync(addr, options);

                    return new Amount(balanceResult.Amount);
                }
                catch (Exception)
                {
                }
            }

            var result = await NknSdk.Wallet.Wallet.GetBalanceAsync(addr, this.Wallet.Options);

            return new Amount(result.Amount);
        }

        public async Task<GetNonceByAddrResult> GetNonceAsync()
        {
            return await this.GetNonceAsync(string.Empty);
        }

        public async Task<GetNonceByAddrResult> GetNonceAsync(string address = null, bool txPool = false)
        {
            var addr = string.IsNullOrEmpty(address) ? this.Wallet.Address : address;

            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {
                    var options = this.Wallet.Options.Clone();
                    options.TxPool = txPool;

                    return await NknSdk.Wallet.Wallet.GetNonceAsync(addr, options);
                }
                catch (Exception)
                {
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options);
            walletOptions.TxPool = txPool;

            return await NknSdk.Wallet.Wallet.GetNonceAsync(addr, walletOptions);
        }

        public async Task<string> SendTransactionAsync(NknSdk.Common.Protobuf.Transaction.Transaction tx)
        {
            if (string.IsNullOrWhiteSpace(this.Wallet.Options.RpcServerAddress))
            {
                try
                {
                    return await NknSdk.Wallet.Wallet.SendTransactionAsync(tx, TransactionOptions.NewFrom(this.Wallet.Options));
                }
                catch (Exception)
                {
                }
            }

            return await NknSdk.Wallet.Wallet.SendTransactionAsync(tx, TransactionOptions.NewFrom(this.options));
        }

        public Task<string> TransferToAsync(string toAddress, decimal amount, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.TransferTo(toAddress, new Amount(amount), this, options);
        }

        public Task<string> RegisterNameAsync(string name, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.RegisterName(name, this, options);
        }

        public Task<string> TransferNameAsync(string name, string recipient, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.TransferName(name, recipient, this, options);
        }

        public Task<string> DeleteNameAsync(string name, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.DeleteName(name, this, options);
        }

        public Task<string> SubscribeAsync(string topic, int duration, string identifier, string meta, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.Subscribe(topic, duration, identifier, meta, this, options);
        }

        public Task<string> UnsubscribeAsync(string topic, string identifier, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.Unsubscribe(topic, identifier, this, options);
        }

        public NknSdk.Common.Protobuf.Transaction.Transaction CreateTransaction(
            NknSdk.Common.Protobuf.Transaction.Payload payload,
            long nonce,
            TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return this.Wallet.CreateTransaction(payload, nonce, options);
        }

        internal async Task<byte[]> SendDataManyAsync<T>(
            IEnumerable<string> destinations,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var payload = MessageFactory.MakeBinaryPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadManyAsync(destinations, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel, timeoutChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendDataAsync<T>(
            string destination,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var payload = MessageFactory.MakeBinaryPayload(data, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destination, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel, timeoutChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendTextAsync<T>(
            string destination,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var payload = MessageFactory.MakeTextPayload(text, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadAsync(destination, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel, timeoutChannel);
                this.AddResponseProcessor(responseProcessor);
            }

            return messageId;
        }

        internal async Task<byte[]> SendTextManyAsync<T>(
            IEnumerable<string> destinations,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var payload = MessageFactory.MakeTextPayload(text, options.ReplyToId, options.MessageId);

            var messageId = await this.SendPayloadManyAsync(destinations, payload, options.IsEncrypted.Value);
            if (messageId != null && options.NoReply != false && responseChannel != null)
            {
                var responseProcessor = new ResponseProcessor<T>(messageId, options.ResponseTimeout, responseChannel, timeoutChannel);
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
            var processedDestination = await this.ProcessDestinationAsync(destination);

            var messagePayload = this.MakeMessageFromPayload(payload, isEncrypted, processedDestination);
            var messagePayloadBytes = messagePayload.ToBytes();

            var messageSize = messagePayloadBytes.Length + processedDestination.Length + Hash.SignatureLength;
            if (messageSize > Constants.MaxClientMessageSize)
            {
                throw new DataSizeTooLargeException($"encoded message is greater than {Constants.MaxClientMessageSize} bytes");
            }

            var message = MessageFactory.MakeOutboundMessage(
                this,
                new List<string> { processedDestination },
                new byte[][] { messagePayloadBytes },
                maxHoldingSeconds);

            this.SendThroughSocket(message.ToBytes());

            return payload.MessageId;
        }

        internal async Task<byte[]> SendPayloadManyAsync(
            IEnumerable<string> destinations,
            Payload payload,
            bool isEncrypted = true,
            uint maxHoldingSeconds = 0)
        {
            if (destinations.Count() == 0)
            {
                return null;
            }
            else if (destinations.Count() == 1)
            {
                return await this.SendPayloadAsync(destinations.First(), payload, isEncrypted, maxHoldingSeconds);
            }

            var processedDestinations = await this.ProcessDestinationManyAsync(destinations);

            var messagePayloads = this.MakeMessageFromPayloadMany(payload, isEncrypted, processedDestinations);
            var messagePayloadsBytes = messagePayloads.Select(x => x.ToBytes()).ToList();

            var currentSize = 0;
            var totalSize = 0;
            var currentDestinations = new List<string>();
            var currentPayloads = new List<byte[]>();
            var messagesToSend = new List<ClientMessage>();

            ClientMessage outboundMessage;

            for (int i = 0; i < messagePayloadsBytes.Count; i++)
            {
                var currentPayload = messagePayloadsBytes[i];
                var currentDstination = processedDestinations[i];

                currentSize = currentPayload.Length + currentDstination.Length + Hash.SignatureLength;
                if (currentSize > Constants.MaxClientMessageSize)
                {
                    throw new DataSizeTooLargeException($"encoded message is greater than {Constants.MaxClientMessageSize} bytes");
                }

                if (totalSize + currentSize > Constants.MaxClientMessageSize)
                {
                    outboundMessage = MessageFactory.MakeOutboundMessage(this, currentDestinations, currentPayloads, maxHoldingSeconds);

                    messagesToSend.Add(outboundMessage);

                    currentDestinations.Clear();
                    currentPayloads.Clear();
                    totalSize = currentSize;
                }

                currentDestinations.Add(currentDstination);
                currentPayloads.Add(currentPayload);

                totalSize += currentSize;
            }

            outboundMessage = MessageFactory.MakeOutboundMessage(this, currentDestinations, currentPayloads, maxHoldingSeconds);

            messagesToSend.Add(outboundMessage);

            foreach (var message in messagesToSend)
            {
                this.SendThroughSocket(message.ToBytes());
            }

            return payload.MessageId;
        }

        internal IList<MessagePayload> MakeMessageFromPayloadMany(Payload payload, bool isEcrypted, IList<string> destinations)
        {
            var payloadBytes = payload.ToBytes();

            if (isEcrypted)
            {
                return this.EncryptPayloadMany(payloadBytes, destinations);
            }

            return new List<MessagePayload> { MessageFactory.MakeMessage(payloadBytes, false) };
        }

        internal MessagePayload MakeMessageFromPayload(Payload payload, bool isEcrypted, string destination)
        {
            var payloadBytes = payload.ToBytes();

            if (isEcrypted)
            {
                return this.EncryptPayload(payloadBytes, destination);
            }

            return MessageFactory.MakeMessage(payloadBytes, false);
        }

        internal void SendAckMany(IEnumerable<string> destinations, byte[] messageId, bool isEncrypted)
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

            this.SendThroughSocket(message.ToBytes());
        }

        internal async Task<IList<string>> ProcessDestinationManyAsync(IEnumerable<string> destinations)
        {
            if (destinations.Count() == 0)
            {
                throw new InvalidDestinationException("no destinations");
            }

            var resultTasks = destinations.Select(this.ProcessDestinationAsync).ToArray();

            var result = (await Task.WhenAll(resultTasks)).Where(x => x.Length > 0).ToList();

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

        private void SendThroughSocket(byte[] data)
        {
            if (this.ws == null)
            {
                throw new ClientNotReadyException();
            }

            this.ws.Send(data);
        }

        private void AddResponseProcessor<T>(ResponseProcessor<T> responseProcessor)
        {
            var typeofResponse = typeof(T);
            if (typeofResponse == Client.stringType)
            {
                this.textResponseManager.Add(responseProcessor as ResponseProcessor<string>);
            }
            else if (typeofResponse == Client.byteArrayType)
            {
                this.binaryResponseManager.Add(responseProcessor as ResponseProcessor<byte[]>);
            }
            else
            {
                throw new InvalidArgumentException(nameof(responseProcessor));
            }
        }

        private void OnWebSocketError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine("[Socket Error] " + e.Message);
        }

        private void OnWebSocketOpen(object sender, EventArgs e)
        {
            var message = new { Action = Constants.MessageActions.SetClient, Addr = this.Address };
            var messageJson = JsonSerializer.ToJsonString(message);

            this.ws.Send(messageJson);

            this.shouldReconnect = true;
            this.reconnectInterval = this.options.ReconnectIntervalMin ?? 0;
        }

        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            if (e.Data[0] != '{')
            {
                var clientMessage = e.RawData.FromBytes<ClientMessage>();
                switch (clientMessage.Type)
                {
                    case ClientMessageType.InboundMessage:
                        Task.Run(() => this.HandleInboundMessageAsync(clientMessage.Message));
                        break;
                    default:
                        break;
                }

                return;
            }

            var message = JsonSerializer.Deserialize<Message>(e.Data);
            if (message.HasError && message.Error != Constants.RpcResponseCodes.Success)
            {
                if (message.Error == Constants.RpcResponseCodes.WrongNode)
                {
                    var wrongNodeMessage = JsonSerializer.Deserialize<WrongNodeMessage>(e.Data);

                    this.OnNewWebSocketAddress(wrongNodeMessage.Result);
                }
                else if (message.Action == Constants.MessageActions.SetClient)
                {
                    try
                    {
                        this.ws.Close();
                    }
                    catch (Exception)
                    {
                    }
                }

                return;
            }

            switch (message.Action)
            {
                case Constants.MessageActions.SetClient:
                    Console.WriteLine("*** Client Connected ***");

                    var setClientMessage = JsonSerializer.Deserialize<SetClientMessage>(e.Data);

                    this.SignatureChainBlockHash = setClientMessage.Result.SigChainBlockHash;

                    if (this.IsReady == false)
                    {
                        this.IsReady = true;

                        if (this.connectHandlers.Count > 0)
                        {
                            foreach (var handler in this.connectHandlers)
                            {
                                var request = new ConnectHandlerRequest { Address = setClientMessage.Result.Node.Address };
                                
                                handler(request);
                            }
                        }
                    }

                    break;

                case Constants.MessageActions.UpdateSigChainBlockHash:
                    var signatureBlockHashMessage = JsonSerializer.Deserialize<UpdateSigChainBlockHashMessage>(e.Data);
                    this.SignatureChainBlockHash = signatureBlockHashMessage.Result;
                    break;

                default: break;
            }
        }

        private void OnWebSocketClosed(object sender, EventArgs e)
        {
            Console.WriteLine("Socket closed.");
            if (this.shouldReconnect)
            {
                Task.Run(this.ReconnectAsync);
            }
        }

        private byte[] GetPayload(MessagePayload message, string source)
            => message.IsEncrypted
                ? this.DecryptPayload(message, source)
                : message.Payload;

        private byte[] DecryptPayload(MessagePayload message, string sourceAddress)
        {
            var rawPayload = message.Payload;
            var sourcePublicKey = Common.Address.AddressToPublicKey(sourceAddress);
            var nonce = message.Nonce;
            var encryptedKey = message.EncryptedKey;

            byte[] decryptedPayload = null;
            if (encryptedKey != null && encryptedKey.Length > 0)
            {
                if (nonce.Length != Hash.NonceLength * 2)
                {
                    throw new DecryptionException("invalid nonce length");
                }

                var nonceFirstHalfBytes = nonce.Take(Hash.NonceLength).ToArray();

                var sharedKey = this.key.Decrypt(encryptedKey, nonceFirstHalfBytes, sourcePublicKey);
                if (sharedKey == null)
                {
                    throw new DecryptionException("decrypt shared key failed");
                }

                var nonceSecondHalfBytes = nonce.Skip(Hash.NonceLength).ToArray();

                decryptedPayload = Hash.DecryptSymmetric(rawPayload, nonceSecondHalfBytes, sharedKey);
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

        private MessagePayload EncryptPayload(byte[] payload, string destination)
        {
            var publicKey = Common.Address.AddressToPublicKey(destination);
            var encryptedKey = this.key.Encrypt(payload, publicKey);

            return MessageFactory.MakeMessage(encryptedKey.Message, true, encryptedKey.Nonce);
        }

        private IList<MessagePayload> EncryptPayloadMany(byte[] payload, IList<string> destinations)
        {
            var nonce = PseudoRandom.RandomBytes(Hash.NonceLength);
            var key = PseudoRandom.RandomBytes(Hash.KeyLength);

            var encryptedPayload = Hash.EncryptSymmetric(payload, nonce, key);

            var result = new List<MessagePayload>();

            for (int i = 0; i < destinations.Count; i++)
            {
                var publicKey = Common.Address.AddressToPublicKey(destinations[i]);
                var encryptedKey = this.Key.Encrypt(key, publicKey);

                var mergedNonce = encryptedKey.Nonce.Concat(nonce).ToArray();

                var message = MessageFactory.MakeMessage(encryptedPayload, true, mergedNonce, encryptedKey.Message);

                result.Add(message);
            }

            return result;
        }

        private async Task ConnectAsync()
        {
            var useTls = this.IsUsingTls;

            for (int i = 0; i < 3; i++)
            {
                GetWsAddressResult getWsAddressResult;
                try
                {
                    if (useTls)
                    {
                        getWsAddressResult = await RpcClient.GetWssAddress(this.options.RpcServerAddress, this.Address);
                    }
                    else
                    {
                        getWsAddressResult = await RpcClient.GetWsAddress(this.options.RpcServerAddress, this.Address);
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                this.OnNewWebSocketAddress(getWsAddressResult);

                return;
            }

            if (this.shouldReconnect)
            {
                await this.ReconnectAsync();
            }
        }

        private async Task<IEnumerable<string>> GetAllSubscribersAsync(string topic, PublishOptions options)
        {
            options = options ?? new PublishOptions();

            var offset = options.Offset;

            var getSubscribersResult = await this.GetSubscribersAsync(topic, options);

            var subscribers = getSubscribersResult.Subscribers;
            var subscribersInTxPool = getSubscribersResult.SubscribersInTxPool;

            var walletOptions = options.Clone();

            while (getSubscribersResult.Subscribers != null && getSubscribersResult.Subscribers.Count() >= options.Limit)
            {
                offset += options.Limit;

                walletOptions.Offset = offset;
                walletOptions.TxPool = false;


                getSubscribersResult = await this.GetSubscribersAsync(topic, walletOptions);

                if (getSubscribersResult.Subscribers != null)
                {
                    subscribers = subscribers.Concat(getSubscribersResult.Subscribers);
                }
            }

            if (options.TxPool == true && subscribersInTxPool != null)
            {
                subscribers = subscribers.Concat(subscribersInTxPool);
            }

            return subscribers;
        }

        private async Task<bool> HandleInboundMessageAsync(byte[] data)
        {
            var inboundMessage = ProtoSerializer.Deserialize<InboundMessage>(data);
            if (inboundMessage.PreviousSignature?.Length > 0)
            {
                var previousSignatureHex = inboundMessage.PreviousSignature.ToHexString();

                var receipt = MessageFactory.MakeReceipt(this.key, previousSignatureHex);
                var receiptBytes = receipt.ToBytes();

                this.SendThroughSocket(receiptBytes);
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
                    this.textResponseManager.ProcessResponse(payload.ReplyToId, null, payload.Type);
                    this.binaryResponseManager.ProcessResponse(payload.ReplyToId, null, payload.Type);
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
                    this.binaryResponseManager.ProcessResponse(payload.ReplyToId, payload.Data, payload.Type);
                }
                else
                {
                    this.textResponseManager.ProcessResponse(payload.ReplyToId, textMessage, payload.Type);
                }

                return true;
            }

            var responses = Enumerable.Empty<object>();
            switch (payload.Type)
            {
                case PayloadType.Binary:
                case PayloadType.Session:
                case PayloadType.Text:

                    var request = new MessageHandlerRequest
                    {
                        Source = inboundMessage.Source,
                        Payload = payload.Data,
                        PayloadType = payload.Type,
                        IsEncrypted = messagePayload.IsEncrypted,
                        MessageId = payload.MessageId,
                        NoReply = payload.NoReply,
                        TextMessage = textMessage
                    };

                    var tasks = this.messageHandlers.Select(async func =>
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

        private static async Task<SendMessageResponse<T>> HandleSendResultTaskAsync<T>(
            Task<byte[]> resultTask,
            SendOptions options,
            Channel<T> responseChannel,
            Channel<T> timeoutChannel)
        {
            var messageId = await resultTask;

            var response = new SendMessageResponse<T> { MessageId = messageId };

            if (options.NoReply == true)
            {
                return response;
            }

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<T>>>
            {
                responseChannel.WaitToRead(cts.Token),
                timeoutChannel.WaitToRead(cts.Token)
            };

            var channel = await channelTasks.FirstAsync(cts);
            if (channel == timeoutChannel)
            {
                response.ErrorMessage = $"A response was not returned in specified time: {options.ResponseTimeout} ms. Request timed out.";

                return response;
            }

            var result = await responseChannel.Reader.ReadAsync().AsTask();

            response.Result = result;

            return response;
        }

        private void OnNewWebSocketAddress(GetWsAddressResult remoteNode)
        {
            if (string.IsNullOrWhiteSpace(remoteNode.Address))
            {
                if (this.shouldReconnect)
                {
                    Task.Run(this.ReconnectAsync);
                }

                return;
            }

            var ws = default(WebSocket);
            var protocol = this.IsUsingTls ? "wss://" : "ws://";
            var uri = $"{protocol}{remoteNode.Address}";

            try
            {
                ws = new WebSocket(uri);
            }
            catch (Exception)
            {
                if (this.shouldReconnect)
                {
                    Task.Run(this.ReconnectAsync);
                }

                return;
            }

            if (this.ws != null)
            {
                this.ws.OnClose -= this.OnWebSocketClosed;
                this.ws.OnMessage -= this.OnWebSocketMessage;
                this.ws.OnError -= this.OnWebSocketError;
                this.ws.OnOpen -= this.OnWebSocketOpen;

                try
                {
                    this.ws.Close();
                }
                catch (Exception)
                {
                }
            }

            this.ws = ws;
            this.RemoteNode = remoteNode;

            this.ws.OnOpen += this.OnWebSocketOpen;
            this.ws.OnMessage += this.OnWebSocketMessage;
            this.ws.OnClose += this.OnWebSocketClosed;
            this.ws.OnError += this.OnWebSocketError;

            this.ws.Connect();
        }

        private async Task ReconnectAsync()
        {
            await Task.Run(async delegate
            {
                await Task.Delay(this.reconnectInterval);
                await this.ConnectAsync();
            });

            this.reconnectInterval *= 2;
            if (this.reconnectInterval > this.options.ReconnectIntervalMax)
            {
                this.reconnectInterval = this.options.ReconnectIntervalMax.Value;
            }
        }
    }
}
