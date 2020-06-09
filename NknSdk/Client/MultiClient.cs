using Ncp;
using NknSdk.Client.Model;
using NknSdk.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static NknSdk.Client.Handlers;
using System.Runtime.Caching;
using NknSdk.Common.Protobuf.Payloads;
using System.Text.RegularExpressions;
using NknSdk.Common.Exceptions;
using Ncp.Exceptions;
using NknSdk.Wallet;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using NknSdk.Common.Extensions;

namespace NknSdk.Client
{
    public class MultiClient
    {
        private readonly MultiClientOptions options;
        private Client defaultClient;
        private readonly CryptoKey key;
        private readonly string identifier;
        private readonly string address;
        private IEnumerable<Regex> acceptedAddresses;
        private readonly IDictionary<string, Session> sessions;
        private bool isReady;
        private bool isClosed;
        private MemoryCache messageCache;
        private IList<Func<MessageHandlerRequest, Task<object>>> messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
        private IList<Func<Session, Task>> sessionListeners = new List<Func<Session, Task>>();
        private IDictionary<string, Client> clients;

        public MultiClient(MultiClientOptions options)
        {
            var baseIdentifier = options.Identifier ?? "";
            options.Identifier = baseIdentifier;

            this.options = options;

            this.InitializeClients();
            
            this.key = this.defaultClient.Key;
            this.identifier = baseIdentifier;
            this.address = (string.IsNullOrWhiteSpace(baseIdentifier) ? "" : baseIdentifier + ".") + this.key.PublicKey;
            
            this.messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.sessionListeners = new List<Func<Session, Task>>();
            this.acceptedAddresses = new List<Regex>();
            this.sessions = new ConcurrentDictionary<string, Session>();
            this.messageCache = new MemoryCache("messageCache");

            this.isReady = false;
            this.isClosed = false;
        }

        public async Task<SendMessageResponse<T>> SendAsync<T>(
            string destination,
            string text,
            SendOptions options = null)
        {
            options ??= new SendOptions();

            var readyClientIds = this.GetReadyClientIds();
            if (readyClientIds.Count() == 0)
            {
                throw new ClientNotReadyException();
            }

            destination = await this.defaultClient.ProcessDestinationAsync(destination);

            try
            {
                var responseChannel = Channel.CreateBounded<T>(1);
                
                var tasks = readyClientIds.Select(x => this.SendWithClient(x, destination, text, options, responseChannel));
                var messageId = await await Task.WhenAny(tasks);

                var response = new SendMessageResponse<T> { MessageId = messageId };

                if (options.NoReply == true)
                {
                    return response;
                }

                var result = await responseChannel.Reader.ReadAsync().AsTask();

                response.Result = result;

                return response;
            }
            catch (Exception)
            {
                return new SendMessageResponse<T> { Error = "failed to send with any client" };
            }
        }

        public async Task<SendMessageResponse<T>> SendAsync<T>(
            string destination, 
            byte[] data, 
            SendOptions options = null)
        {
            options ??= new SendOptions();

            var readyClientIds = this.GetReadyClientIds();
            if (readyClientIds.Count() == 0)
            {
                throw new ClientNotReadyException();
            }

            destination = await this.defaultClient.ProcessDestinationAsync(destination);

            try
            {
                var responseChannel = Channel.CreateBounded<T>(1);
                
                var tasks = readyClientIds.Select(x => this.SendWithClient(x, destination, data, options, responseChannel));
                var messageId = await await Task.WhenAny(tasks);

                var response = new SendMessageResponse<T> { MessageId = messageId };

                if (options.NoReply == true)
                {
                    return response;
                }

                var result = await responseChannel.Reader.ReadAsync().AsTask();

                response.Result = result;

                return response;
            }
            catch (Exception)
            {
                return new SendMessageResponse<T> { Error = "failed to send with any client" };
            }
        }

        public async Task<byte[]> SendToManyAsync(
            IList<string> destinations,
            byte[] data,
            SendOptions options = null)
        {
            options ??= new SendOptions();

            var readyClientIds = this.GetReadyClientIds();
            if (readyClientIds.Count() == 0)
            {
                throw new ClientNotReadyException();
            }

            destinations = await this.defaultClient.ProcessDestinationsAsync(destinations);

            try
            {
                var tasks = readyClientIds.Select(x => this.SendWithClient<byte[]>(x, destinations, data, options));
                
                var messageId = await await Task.WhenAny(tasks);

                return messageId;
            }
            catch (Exception)
            {
                throw new ApplicationException("failed to send with any client");
            }
        }

        public async Task<byte[]> SendToManyAsync(
            IList<string> destinations,
            string text,
            SendOptions options = null)
        {
            options ??= new SendOptions();

            var readyClientIds = this.GetReadyClientIds();
            if (readyClientIds.Count() == 0)
            {
                throw new ClientNotReadyException();
            }

            destinations = await this.defaultClient.ProcessDestinationsAsync(destinations);

            try
            {
                var tasks = readyClientIds.Select(x => this.SendWithClient<byte[]>(x, destinations, text, options));
                
                var messageId = await await Task.WhenAny(tasks);

                return messageId;
            }
            catch (Exception)
            {
                throw new ApplicationException("failed to send with any client");
            }
        }

        public void OnMessage(Func<MessageHandlerRequest, Task<object>> func)
        {
            this.messageListeners.Add(func);
        }

        /// <summary>
        /// Adds a callback that will be executed when client accepts a new session
        /// </summary>
        /// <param name="func">The callback to be executed</param>
        public void OnSession(Func<Session, Task> func)
        {
            this.sessionListeners.Add(func);
        }

        public void OnConnect(ConnectHandler func)
        {
            var tasks = this.clients.Keys.Select(x =>
            {
                return Task.Run(async () =>
                {
                    var connectChannel = Channel.CreateBounded<ConnectRequest>(1);
                    this.clients[x].OnConnect(async (req) =>
                    {
                        await connectChannel.Writer.WriteAsync(req);
                    });

                    return await connectChannel.Reader.ReadAsync();
                });
            });

            try
            {
                Task.WhenAny(tasks).ContinueWith(async (task) =>
                {
                    var request = await await task;

                    func(request);

                    this.isReady = true;
                });
            }
            catch (Exception)
            {
                this.CloseAsync();
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

        /// <summary>
        /// Dial a session to a remote NKN address.
        /// </summary>
        /// <param name="remoteAddress">The address to open session with</param>
        /// <param name="sessionConfiguration">Session configuration options</param>
        /// <returns>The session object</returns>
        public async Task<Session> DialAsync(string remoteAddress, SessionConfiguration sessionConfiguration)
        {
            var dialTimeout = Ncp.Constants.DefaultInitialRetransmissionTimeout;

            var sessionId = PseudoRandom.RandomBytesAsHexString(Constants.SessionIdSize);
            //var sessionId = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }.ToHexString();

            var session = this.MakeSession(remoteAddress, sessionId, sessionConfiguration);

            var sessionKey = Session.GetKey(remoteAddress, sessionId);

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

            foreach (var clientId in this.clients.Keys)
            {
                try
                {
                    this.clients[clientId].Close();
                }
                catch (Exception)
                {
                }

                this.messageCache.Dispose();
                this.isClosed = true;
            }
        }

        private void InitializeClients()
        {
            var clients = new ConcurrentDictionary<string, Client>();
            if (options.OriginalClient)
            {
                var clientId = Client.AddIdentifier("", "");
                clients[clientId] = new Client(options);

                if (string.IsNullOrWhiteSpace(options.Seed))
                {
                    options.Seed = clients[clientId].Key.Seed;
                }
            }

            for (int i = 0; i < options.NumberOfSubClients; i++)
            {
                var clientId = Client.AddIdentifier("", i.ToString());
                options.Identifier = Client.AddIdentifier(this.options.Identifier, i.ToString());
                clients[clientId] = new Client(options);

                if (i == 0 && string.IsNullOrWhiteSpace(options.Seed))
                {
                    options.Seed = clients[clientId].Seed;
                }
            }

            var clientIds = clients.Keys.OrderBy(x => x);
            if (clientIds.Count() == 0)
            {
                throw new InvalidArgumentException("should have at least one client");
            }

            foreach (var clientId in clientIds)
            {
                clients[clientId].OnMessage(message => this.HandleClientMessage(message, clientId));
            }

            this.clients = clients;
            this.defaultClient = clients[clientIds.First()];
        }

        private async Task<object> HandleClientMessage(MessageHandlerRequest message, string clientId)
        {
            if (this.isClosed)
            {
                return false;
            }

            if (message.PayloadType == PayloadType.Session)
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
            if (this.messageCache.Get(messageKey) != null)
            {
                return false;
            }

            var expiration = DateTime.Now.AddSeconds(options.MessageHoldingSeconds.Value);
            this.messageCache.Set(messageKey, clientId, expiration);

            var removeIdentifierResult = Client.RemoveIdentifier(message.Source);
            message.Source = removeIdentifierResult.Address;

            var responses = Enumerable.Empty<object>();

            if (this.messageListeners.Any())
            {
                var tasks = this.messageListeners.Select(async func =>
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
                            return true;
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
                    foreach (var client in this.clients)
                    {
                        if (client.Value.IsReady)
                        {
                            client.Value.SendAck(
                                Client.AddIdentifierPrefix(message.Source, client.Key),
                                message.MessageId,
                                message.IsEncrypted);
                        }
                    }
                }
            }

            return false;
        }

        private async Task<byte[]> SendWithClient<T>(
            string clientId,
            string destination,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var client = this.clients[clientId];
            if (client == null)
            {
                throw new InvalidArgumentException("no such clientId");
            }

            if (client.IsReady == false)
            {
                throw new ClientNotReadyException();
            }

            var messageId = await client.SendTextAsync(
                Client.AddIdentifierPrefix(destination, clientId),
                text,
                options,
                responseChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClient<T>(
            string clientId,
            string destination,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var client = this.clients[clientId];
            if (client == null)
            {
                throw new InvalidArgumentException("no such clientId");
            }

            if (client.IsReady == false)
            {
                throw new ClientNotReadyException();
            }

            var messageId = await client.SendDataAsync(
                Client.AddIdentifierPrefix(destination, clientId),
                data,
                options,
                responseChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClient<T>(
            string clientId,
            IList<string> destinations,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var client = this.clients[clientId];
            if (client == null)
            {
                throw new InvalidArgumentException("no such clientId");
            }

            if (client.IsReady == false)
            {
                throw new ClientNotReadyException();
            }

            var messageId = await client.SendDataAsync(
                destinations.Select(x => Client.AddIdentifierPrefix(x, clientId)).ToList(),
                data,
                options,
                responseChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClient<T>(
            string clientId,
            IList<string> destinations,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null)
        {
            var client = this.clients[clientId];
            if (client == null)
            {
                throw new InvalidArgumentException("no such clientId");
            }

            if (client.IsReady == false)
            {
                throw new ClientNotReadyException();
            }

            var messageId = await client.SendTextAsync(
                destinations.Select(x => Client.AddIdentifierPrefix(x, clientId)).ToList(),
                text,
                options,
                responseChannel);

            return messageId;
        }

        private bool IsAcceptedAddress(string address)
        {
            foreach (var item in this.acceptedAddresses)
            {
                if (item.IsMatch(address))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task HandleSessionMessageAsync(string clientId, string source, byte[] sessionId, byte[] data)
        {
            var remote = Client.RemoveIdentifier(source);
            var remoteAddress = remote.Address;
            var remoteClientId = remote.ClientId;
            var sessionKey = Session.GetKey(remoteAddress, sessionId.ToHexString());

            Session session;
            bool existed = false;

            lock (this)
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

                    session = this.MakeSession(remoteAddress, sessionId.ToHexString(), this.options.SessionConfiguration);
                    this.sessions.Add(sessionKey, session);
                }
            }

            await session.ReceiveWithAsync(clientId, remoteClientId, data);

            if (existed == false)
            {
                await session.AcceptAsync();

                if (this.sessionListeners.Count > 0)
                {
                    var tasks = this.sessionListeners.Select(async func =>
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

        private Session MakeSession(string remoteAddress, string sessionId, SessionConfiguration configuration)
        {
            var clientIds = this.GetReadyClientIds().OrderBy(x => x).ToArray();
            return new Session(
                this.address,
                remoteAddress,
                clientIds,
                null,
                async (string localClientId, string remoteClientId, byte[] data) =>
                {
                    var client = this.clients[localClientId];
                    if (client.IsReady == false)
                    {
                        throw new ClientNotReadyException();
                    }

                    var payload = MessageFactory.MakeSessionPayload(data, sessionId);
                    var destination = Client.AddIdentifierPrefix(remoteAddress, remoteClientId);

                    await client.SendPayloadAsync(destination, payload);
                },
                configuration);
        }

        private IEnumerable<string> GetReadyClientIds()
            => this.clients.Keys.Where(x => this.clients.ContainsKey(x) && this.clients[x].IsReady);
    }
}
