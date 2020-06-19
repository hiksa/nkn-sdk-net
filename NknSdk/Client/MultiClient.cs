using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.Caching;

using Ncp;
using Ncp.Exceptions;

using NknSdk.Client.Requests;
using NknSdk.Common;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Extensions;
using NknSdk.Wallet.Models;
using NknSdk.Common.Options;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Rpc;
using NknSdk.Common.Rpc.Results;

using Constants = NknSdk.Common.Constants;

namespace NknSdk.Client
{
    public class MultiClient : ITransactionSender
    {
        private readonly object syncLock = new object();
        private readonly IDictionary<string, Session> sessions;
        private readonly MultiClientOptions options;
        private readonly CryptoKey key;

        private bool isReady;
        private bool isClosed;
        private string identifier;
        private MemoryCache messageCache;
        private IEnumerable<Regex> acceptedAddresses;
        private IList<Func<Session, Task>> sessionHandlers = new List<Func<Session, Task>>();
        private IList<Func<MessageHandlerRequest, Task<object>>> messageHandlers = new List<Func<MessageHandlerRequest, Task<object>>>();

        public MultiClient(MultiClientOptions options = null)
        {
            options = options ?? new MultiClientOptions();

            options.Identifier = options.Identifier ?? "";

            this.options = options;

            this.InitializeClients();

            this.key = this.DefaultClient.Key;
            this.Address = (string.IsNullOrWhiteSpace(options.Identifier) ? "" : options.Identifier + ".") + this.key.PublicKey;

            this.messageHandlers = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.sessionHandlers = new List<Func<Session, Task>>();
            this.acceptedAddresses = new List<Regex>();
            this.sessions = new ConcurrentDictionary<string, Session>();
            this.messageCache = new MemoryCache("messageCache");

            this.isReady = false;
            this.isClosed = false;
        }

        public string PublicKey => this.key.PublicKey;

        public string Address { get; }

        public IDictionary<string, Client> Clients { get; private set; }

        public Client DefaultClient { get; private set; }

        public void OnMessage(Func<MessageHandlerRequest, Task<object>> func) => this.messageHandlers.Add(func);

        /// <summary>
        /// Adds a callback that will be executed when client accepts a new session
        /// </summary>
        /// <param name="func">The callback to be executed</param>
        public void OnSession(Func<Session, Task> func) => this.sessionHandlers.Add(func);

        public void OnConnect(Action<ConnectHandlerRequest> func)
        {
            var tasks = this.Clients.Keys.Select(clientId =>
            {
                return Task.Run(async () =>
                {
                    var connectChannel = Channel.CreateBounded<ConnectHandlerRequest>(1);

                    this.Clients[clientId].OnConnect(async request => await connectChannel.Writer.WriteAsync(request));

                    return await connectChannel.Reader.ReadAsync();
                });
            });

            try
            {
                Task.WhenAny(tasks).ContinueWith(async task =>
                {
                    var request = await await task;

                    func(request);

                    this.isReady = true;
                });
            }
            catch (Exception)
            {
                Task.Run(this.CloseAsync);
            }
        }

        /// <summary>
        /// Start accepting sessions from addresses.
        /// </summary>
        /// <param name="addresses"></param>
        public void Listen(IEnumerable<string> addresses = null)
        {
            if (addresses == null)
            {
                this.Listen(new[] { Constants.DefaultSessionAllowedAddressRegex });
            }
            else
            {
                this.Listen(addresses.Select(x => new Regex(x)));
            }
        }

        /// <summary>
        /// Start accepting sessions from addresses.
        /// </summary>
        /// <param name="addresses"></param>
        public void Listen(IEnumerable<Regex> addresses = null)
        {
            if (addresses == null)
            {
                addresses = new Regex[] { Constants.DefaultSessionAllowedAddressRegex };
            }

            this.acceptedAddresses = addresses;
        }

        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            string destination,
            string text,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            var readyClientIds = this.GetReadyClientIds();

            destination = await this.DefaultClient.ProcessDestinationAsync(destination);

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTasks = readyClientIds.Select(clientId => this.SendWithClientAsync(
                    clientId,
                    destination,
                    text,
                    options,
                    responseChannel,
                    timeoutChannel));

                return await HandleSendMessageTasksAsync(resultTasks, options, responseChannel, timeoutChannel);
            }
            catch (Exception)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send with any client" };
            }
        }

        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            string destination,
            byte[] data,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            var readyClientIds = this.GetReadyClientIds();

            destination = await this.DefaultClient.ProcessDestinationAsync(destination);

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTasks = readyClientIds.Select(clientId => this.SendWithClientAsync(
                    clientId,
                    destination,
                    data,
                    options,
                    responseChannel,
                    timeoutChannel));

                return await HandleSendMessageTasksAsync(resultTasks, options, responseChannel, timeoutChannel);
            }
            catch (Exception e)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send with any client" };
            }
        }

        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            IEnumerable<string> destinations,
            byte[] data,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            var readyClientIds = this.GetReadyClientIds();

            destinations = await this.DefaultClient.ProcessDestinationManyAsync(destinations);

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var sendMessageTasks = readyClientIds.Select(clientId => this.SendWithClientAsync(
                    clientId,
                    destinations,
                    data,
                    options,
                    responseChannel,
                    timeoutChannel));

                return await HandleSendMessageTasksAsync(sendMessageTasks, options, responseChannel, timeoutChannel);
            }
            catch (Exception e)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send with any client" };
            }
        }

        public async Task<SendMessageResponse<TResponse>> SendAsync<TResponse>(
            IEnumerable<string> destinations,
            string text,
            SendOptions options = null)
        {
            options = options ?? new SendOptions();

            var readyClientIds = this.GetReadyClientIds();

            destinations = await this.DefaultClient.ProcessDestinationManyAsync(destinations);

            try
            {
                var responseChannel = Channel.CreateBounded<TResponse>(1);
                var timeoutChannel = Channel.CreateBounded<TResponse>(1);

                var resultTasks = readyClientIds.Select(clientId => this.SendWithClientAsync(
                    clientId,
                    destinations,
                    text,
                    options,
                    responseChannel,
                    timeoutChannel));

                return await HandleSendMessageTasksAsync(resultTasks, options, responseChannel, timeoutChannel);
            }
            catch (Exception e)
            {
                return new SendMessageResponse<TResponse> { ErrorMessage = "failed to send with any client" };
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

            return await this.SendAsync<byte[]>(subscribers.ToList(), text);
        }

        public async Task<SendMessageResponse<byte[]>> PublishAsync(
            string topic,
            byte[] data,
            PublishOptions options = null)
        {
            options = options ?? new PublishOptions();
            options.NoReply = true;

            var subscribers = await this.GetAllSubscribersAsync(topic, options);

            return await this.SendAsync<byte[]>(subscribers.ToList(), data);
        }

        /// <summary>
        /// Dial a session to a remote NKN address.
        /// </summary>
        /// <param name="remoteAddress">The address to open session with</param>
        /// <param name="options">Session configuration options</param>
        /// <returns>The session object</returns>
        public async Task<Session> DialAsync(string remoteAddress, SessionOptions options = null)
        {
            options = options ?? new SessionOptions();

            var dialTimeout = Ncp.Constants.DefaultInitialRetransmissionTimeout;

            var sessionId = PseudoRandom.RandomBytesAsHexString(Constants.SessionIdLength);

            var session = this.MakeSession(remoteAddress, sessionId, options);

            var sessionKey = Session.MakeKey(remoteAddress, sessionId);

            this.sessions.Add(sessionKey, session);

            await session.DialAsync(dialTimeout);

            return session;
        }

        /// <summary>
        /// Close the client and all sessions.
        /// </summary>
        /// <returns></returns>
        public async Task CloseAsync()
        {
            var tasks = this.sessions.Values.Select(x => x.CloseAsync());

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
            }

            foreach (var clientId in this.Clients.Keys)
            {
                try
                {
                    this.Clients[clientId].Close();
                }
                catch (Exception)
                {
                }

                this.messageCache.Dispose();
                this.isClosed = true;
            }
        }

        public async Task<GetLatestBlockHashResult> GetLatestBlockAsync()
        {
            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        return await Wallet.Wallet.GetLatestBlockAsync(this.Clients[clientId].Wallet.Options);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return await Wallet.Wallet.GetLatestBlockAsync(WalletOptions.NewFrom(this.options));
        }

        public async Task<GetRegistrantResult> GetRegistrantAsync(string name)
        {
            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        return await Wallet.Wallet.GetRegistrantAsync(name, this.Clients[clientId].Wallet.Options);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return await Wallet.Wallet.GetRegistrantAsync(name, WalletOptions.NewFrom(this.options));
        }

        public async Task<GetSubscribersResult> GetSubscribersAsync(string topic, PublishOptions options = null)
        {
            options = options ?? new PublishOptions();

            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        var mergedOptions = this.Clients[clientId].Wallet.Options.MergeWith(options);
                        return await Wallet.Wallet.GetSubscribersAsync(topic, mergedOptions);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options).AssignFrom(options);
            return await Wallet.Wallet.GetSubscribersAsync(topic, walletOptions);
        }

        public async Task<int> GetSubscribersCountAsync(string topic)
        {
            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        return await Wallet.Wallet.GetSubscribersCountAsync(topic, this.Clients[clientId].Wallet.Options);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return await Wallet.Wallet.GetSubscribersCountAsync(topic, WalletOptions.NewFrom(this.options));
        }

        public async Task<GetSubscriptionResult> GetSubscriptionAsync(string topic, string subscriber)
        {
            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        return await Wallet.Wallet.GetSubscriptionAsync(
                            topic,
                            subscriber,
                            this.Clients[clientId].Wallet.Options);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return await Wallet.Wallet.GetSubscriptionAsync(
                topic,
                subscriber,
                WalletOptions.NewFrom(this.options));
        }

        public async Task<Amount> GetBalanceAsync(string address = null)
        {
            var addr = string.IsNullOrEmpty(address) ? this.DefaultClient.Wallet.Address : address;

            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        var balanceResult = await Wallet.Wallet.GetBalanceAsync(addr, this.Clients[clientId].Wallet.Options);

                        return new Amount(balanceResult.Amount);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            var result = await Wallet.Wallet.GetBalanceAsync(addr, WalletOptions.NewFrom(this.options));

            return new Amount(result.Amount);
        }

        public async Task<GetNonceByAddrResult> GetNonceAsync()
        {
            return await this.GetNonceAsync(string.Empty);
        }

        public async Task<GetNonceByAddrResult> GetNonceAsync(string address = null, bool txPool = false)
        {
            var addr = string.IsNullOrEmpty(address) ? this.DefaultClient.Wallet.Address : address;

            foreach (var clientId in this.Clients.Keys)
            {
                if (string.IsNullOrEmpty(this.Clients[clientId].Wallet.Options.RpcServerAddress) == false)
                {
                    try
                    {
                        var options = this.Clients[clientId].Wallet.Options.Clone();
                        options.TxPool = txPool;

                        var getNonceResult = await Wallet.Wallet.GetNonceAsync(addr, options);

                        return getNonceResult;
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            var walletOptions = WalletOptions.NewFrom(this.options);
            walletOptions.TxPool = txPool;

            return await Wallet.Wallet.GetNonceAsync(addr, walletOptions);
        }

        public async Task<string> SendTransactionAsync(Transaction tx)
        {
            var clients = this.Clients.Values.Where(x => string.IsNullOrEmpty(x.Wallet.Options.RpcServerAddress) == false);
            if (clients.Count() > 0)
            {
                var sendTransactionTasks = clients
                    .Select(x => Wallet.Wallet.SendTransactionAsync(tx, TransactionOptions.NewFrom(x.Wallet.Options)));

                return await await Task.WhenAny(sendTransactionTasks);
            }

            return await Wallet.Wallet.SendTransactionAsync(tx, TransactionOptions.NewFrom(this.options));
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

        public Task<string> DeleteNameAsync(string name, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();

            return RpcClient.DeleteName(name, this, options);
        }

        public Task<string> SubscribeAsync(
            string topic,
            int duration,
            string identifier,
            string meta,
            TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();

            return RpcClient.Subscribe(topic, duration, identifier, meta, this, options);
        }

        public Task<string> UnsubscribeAsync(string topic, string identifier, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();

            return RpcClient.Unsubscribe(topic, identifier, this, options);
        }

        public Transaction CreateTransaction(Payload payload, long nonce, TransactionOptions options)
        {
            return this.DefaultClient.Wallet.CreateTransaction(payload, nonce, options);
        }

        private void InitializeClients()
        {
            var baseIdentifier = string.IsNullOrEmpty(this.options.Identifier) ? "" : this.options.Identifier;
            var clients = new ConcurrentDictionary<string, Client>();

            if (this.options.OriginalClient)
            {
                var clientId = Common.Address.AddIdentifier("", "");
                clients[clientId] = new Client(this.options);

                if (string.IsNullOrWhiteSpace(this.options.SeedHex))
                {
                    this.options.SeedHex = clients[clientId].Key.SeedHex;
                }
            }

            for (int i = 0; i < this.options.NumberOfSubClients; i++)
            {
                var clientId = Common.Address.AddIdentifier("", i.ToString());

                this.options.Identifier = Common.Address.AddIdentifier(baseIdentifier, i.ToString());

                clients[clientId] = new Client(this.options);

                if (i == 0 && string.IsNullOrWhiteSpace(this.options.SeedHex))
                {
                    this.options.SeedHex = clients[clientId].SeedHex;
                }
            }

            var clientIds = clients.Keys.OrderBy(x => x);
            if (clientIds.Count() == 0)
            {
                throw new InvalidArgumentException("should have at least one client");
            }

            foreach (var clientId in clients.Keys)
            {
                clients[clientId].OnMessage(async message =>
                {
                    if (this.isClosed)
                    {
                        return false;
                    }

                    if (message.PayloadType == Common.Protobuf.Payloads.PayloadType.Session)
                    {
                        if (!message.IsEncrypted)
                        {
                            return false;
                        }

                        try
                        {
                            await this.HandleSessionMessageAsync(clientId, message.Source, message.MessageId, message.Payload);
                        }
                        catch (AddressNotAllowedException)
                        {
                        }
                        catch (SessionClosedException)
                        {
                        }
                        catch (Exception ex)
                        {
                        }

                        return false;
                    }

                    var messageKey = message.MessageId.ToHexString();
                    lock (this.syncLock)
                    {
                        if (this.messageCache.Contains(messageKey))
                        {
                            return false;
                        }

                        var expiration = DateTime.Now.AddSeconds(options.MessageCacheExpiration);
                        this.messageCache.Set(messageKey, clientId, expiration);
                    }

                    var removeIdentifierResult = Common.Address.RemoveIdentifier(message.Source);
                    message.Source = removeIdentifierResult.Address;

                    var responses = Enumerable.Empty<object>();

                    if (this.messageHandlers.Any())
                    {
                        var tasks = this.messageHandlers.Select(async func =>
                        {
                            try
                            {
                                var result = await func(message);

                                return result;
                            }
                            catch (Exception)
                            {
                                return null;
                            }
                        });

                        responses = await Task.WhenAll(tasks);
                    }

                    if (message.NoReply == false)
                    {
                        var responded = false;
                        foreach (var response in responses)
                        {
                            if (response is bool res)
                            {
                                if (res == false)
                                {
                                    return false;
                                }
                            }
                            else if (response != null)
                            {
                                if (response is byte[] bytes)
                                {
                                    await this.SendAsync<byte[]>(
                                        message.Source,
                                        bytes,
                                        new SendOptions
                                        {
                                            IsEncrypted = message.IsEncrypted,
                                            HoldingSeconds = 0,
                                            ReplyToId = message.MessageId.ToHexString()
                                        });

                                    responded = true;
                                }
                                else if (response is string text)
                                {
                                    await this.SendAsync<byte[]>(
                                        message.Source,
                                        text,
                                        new SendOptions
                                        {
                                            IsEncrypted = message.IsEncrypted,
                                            HoldingSeconds = 0,
                                            ReplyToId = message.MessageId.ToHexString()
                                        });

                                    responded = true;
                                }
                            }
                        }

                        if (responded == false)
                        {
                            foreach (var client in this.Clients)
                            {
                                if (client.Value.IsReady)
                                {
                                    client.Value.SendAck(
                                        Common.Address.AddIdentifierPrefix(message.Source, client.Key),
                                        message.MessageId,
                                        message.IsEncrypted);
                                }
                            }
                        }
                    }

                    return false;
                });
            }

            this.Clients = clients;
            this.identifier = baseIdentifier;
            this.options.Identifier = baseIdentifier;
            this.DefaultClient = clients[clientIds.First()];
        }

        private bool IsAcceptedAddress(string address)
        {
            foreach (var pattern in this.acceptedAddresses)
            {
                if (pattern.IsMatch(address))
                {
                    return true;
                }
            }

            return false;
        }

        private Session MakeSession(string remoteAddress, string sessionId, SessionOptions options)
        {
            var clientIds = this.GetReadyClientIds().OrderBy(x => x).ToArray();

            return new Session(
                this.Address,
                remoteAddress,
                clientIds,
                null,
                SendSessionDataHandler,
                options);

            async Task SendSessionDataHandler(string localClientId, string remoteClientId, byte[] data)
            {
                var client = this.Clients[localClientId];
                if (client.IsReady == false)
                {
                    throw new ClientNotReadyException();
                }

                var payload = MessageFactory.MakeSessionPayload(data, sessionId);
                var destination = Common.Address.AddIdentifierPrefix(remoteAddress, remoteClientId);

                await client.SendPayloadAsync(destination, payload);
            }
        }

        private IEnumerable<string> GetReadyClientIds()
        {
            var readyClientIds = this.Clients.Keys.Where(clientId => this.Clients[clientId].IsReady);

            if (readyClientIds.Count() == 0)
            {
                throw new ClientNotReadyException();
            }

            return readyClientIds;
        }

        private async Task<IEnumerable<string>> GetAllSubscribersAsync(string topic, PublishOptions options)
        {
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

        private static async Task<SendMessageResponse<T>> HandleSendMessageTasksAsync<T>(
            IEnumerable<Task<byte[]>> resultTasks,
            SendOptions options, 
            Channel<T> responseChannel, 
            Channel<T> timeoutChannel)
        {
            var messageId = await await Task.WhenAny(resultTasks);

            var response = new SendMessageResponse<T> { MessageId = messageId };

            if (options.NoReply == true || messageId == null)
            {
                responseChannel.Writer.TryComplete();
                timeoutChannel.Writer.TryComplete();

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

        private async Task HandleSessionMessageAsync(string clientId, string source, byte[] sessionId, byte[] data)
        {
            var remote = Common.Address.RemoveIdentifier(source);
            var remoteAddress = remote.Address;
            var remoteClientId = remote.ClientId;
            var sessionKey = Session.MakeKey(remoteAddress, sessionId.ToHexString());

            Session session;
            bool existed = false;

            lock (this.syncLock)
            {
                existed = this.sessions.ContainsKey(sessionKey);
                if (existed)
                {
                    session = this.sessions[sessionKey];
                }
                else
                {
                    if (this.IsAcceptedAddress(remoteAddress) == false)
                    {
                        throw new AddressNotAllowedException();
                    }

                    session = this.MakeSession(remoteAddress, sessionId.ToHexString(), this.options.SessionOptions);

                    this.sessions.Add(sessionKey, session);
                }
            }

            await session.ReceiveWithClientAsync(clientId, remoteClientId, data);

            if (existed == false)
            {
                await session.AcceptAsync();

                if (this.sessionHandlers.Count > 0)
                {
                    var tasks = this.sessionHandlers.Select(async func =>
                    {
                        try
                        {
                            await func(session);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Session handler error " + ex.Message);
                            return;
                        }
                    });

                    await Task.WhenAll(tasks);
                }
            }
        }

        private async Task<byte[]> SendWithClientAsync<T>(
            string clientId,
            string destination,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var client = this.GetClient(clientId);

            var messageId = await client.SendTextAsync(
                Common.Address.AddIdentifierPrefix(destination, clientId),
                text,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClientAsync<T>(
            string clientId,
            string destination,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var client = this.GetClient(clientId);

            var messageId = await client.SendDataAsync(
                Common.Address.AddIdentifierPrefix(destination, clientId),
                data,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClientAsync<T>(
            string clientId,
            IEnumerable<string> destinations,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var client = this.GetClient(clientId);

            var messageId = await client.SendDataManyAsync(
                destinations.Select(x => Common.Address.AddIdentifierPrefix(x, clientId)),
                data,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClientAsync<T>(
            string clientId,
            IEnumerable<string> destinations,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
        {
            var client = this.GetClient(clientId);

            var messageId = await client.SendTextManyAsync(
                destinations.Select(x => Common.Address.AddIdentifierPrefix(x, clientId)),
                text,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }

        private Client GetClient(string clientId)
        {
            if (this.Clients.ContainsKey(clientId) == false)
            {
                throw new InvalidArgumentException($"no client with such clientId: {clientId}");
            }

            var client = this.Clients[clientId];
            if (client == null)
            {
                throw new InvalidArgumentException($"no client with such clientId: {clientId}");
            }            

            if (client.IsReady == false)
            {
                throw new ClientNotReadyException();
            }

            return client;
        }
    }
}
