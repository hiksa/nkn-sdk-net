﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Ncp;
using Ncp.Exceptions;

using NknSdk.Common;
using NknSdk.Common.Protobuf.Payloads;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Extensions;
using NknSdk.Client.Network;

using static NknSdk.Client.Network.Handlers;

namespace NknSdk.Client
{
    public class MultiClient
    {
        private readonly IDictionary<string, Session> sessions;
        private readonly MultiClientOptions options;
        private readonly CryptoKey key;
        private readonly string identifier;
        private readonly string address;

        private bool isReady;
        private bool isClosed;
        private Client defaultClient;
        private MemoryCache messageCache;
        private IEnumerable<Regex> acceptedAddresses;
        private IDictionary<string, Client> clients;
        private IList<Func<Session, Task>> sessionHandlers = new List<Func<Session, Task>>();
        private IList<Func<MessageHandlerRequest, Task<object>>> messageHandlers = new List<Func<MessageHandlerRequest, Task<object>>>();

        public MultiClient(MultiClientOptions options)
        {
            var baseIdentifier = options.Identifier ?? "";
            options.Identifier = baseIdentifier;

            this.options = options;

            this.InitializeClients();
            
            this.key = this.defaultClient.Key;
            this.identifier = baseIdentifier;
            this.address = (string.IsNullOrWhiteSpace(baseIdentifier) ? "" : baseIdentifier + ".") + this.key.PublicKey;
            
            this.messageHandlers = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.sessionHandlers = new List<Func<Session, Task>>();
            this.acceptedAddresses = new List<Regex>();
            this.sessions = new ConcurrentDictionary<string, Session>();
            this.messageCache = new MemoryCache("messageCache");

            this.isReady = false;
            this.isClosed = false;
        }

        public void OnMessage(Func<MessageHandlerRequest, Task<object>> func) => this.messageHandlers.Add(func);

        /// <summary>
        /// Adds a callback that will be executed when client accepts a new session
        /// </summary>
        /// <param name="func">The callback to be executed</param>
        public void OnSession(Func<Session, Task> func) => this.sessionHandlers.Add(func);

        public void OnConnect(ConnectHandler func)
        {
            var tasks = this.clients.Keys.Select(x =>
            {
                return Task.Run(async () =>
                {
                    var connectChannel = Channel.CreateBounded<ConnectHandlerRequest>(1);

                    this.clients[x].OnConnect(async req => await connectChannel.Writer.WriteAsync(req));

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
                
                var tasks = readyClientIds.Select(x => this.SendWithClientAsync(x, destination, text, options, responseChannel));
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
                
                var tasks = readyClientIds.Select(x => this.SendWithClientAsync(x, destination, data, options, responseChannel));
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
                var tasks = readyClientIds.Select(x => this.SendWithClientAsync<byte[]>(x, destinations, data, options));
                
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
                var tasks = readyClientIds.Select(x => this.SendWithClientAsync<byte[]>(x, destinations, text, options));
                
                var messageId = await await Task.WhenAny(tasks);

                return messageId;
            }
            catch (Exception)
            {
                throw new ApplicationException("failed to send with any client");
            }
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

            var sessionId = PseudoRandom.RandomBytesAsHexString(Constants.SessionIdLength);

            var session = this.MakeSession(remoteAddress, sessionId, sessionConfiguration);

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
                var clientId = Address.AddIdentifier("", "");
                clients[clientId] = new Client(options);

                if (string.IsNullOrWhiteSpace(options.Seed))
                {
                    options.Seed = clients[clientId].Key.Seed;
                }
            }

            for (int i = 0; i < options.NumberOfSubClients; i++)
            {
                var clientId = Address.AddIdentifier("", i.ToString());
                options.Identifier = Address.AddIdentifier(this.options.Identifier, i.ToString());
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
                clients[clientId].OnMessage(message => this.HandleClientMessageAsync(message, clientId));
            }

            this.clients = clients;
            this.defaultClient = clients[clientIds.First()];
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

        private Session MakeSession(string remoteAddress, string sessionId, SessionConfiguration configuration)
        {
            var clientIds = this.GetReadyClientIds().OrderBy(x => x).ToArray();

            return new Session(
                this.address,
                remoteAddress,
                clientIds,
                null,
                SendSessionDataAsync,
                configuration);

            async Task SendSessionDataAsync(string localClientId, string remoteClientId, byte[] data)
            {
                var client = this.clients[localClientId];
                if (client.IsReady == false)
                {
                    throw new ClientNotReadyException();
                }

                var payload = MessageFactory.MakeSessionPayload(data, sessionId);
                var destination = Address.AddIdentifierPrefix(remoteAddress, remoteClientId);

                await client.SendPayloadAsync(destination, payload);
            }
        }

        private IEnumerable<string> GetReadyClientIds()
            => this.clients.Keys.Where(x => this.clients.ContainsKey(x) && this.clients[x].IsReady);

        private async Task<object> HandleClientMessageAsync(MessageHandlerRequest message, string clientId)
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

            var removeIdentifierResult = Address.RemoveIdentifier(message.Source);
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
                                Address.AddIdentifierPrefix(message.Source, client.Key),
                                message.MessageId,
                                message.IsEncrypted);
                        }
                    }
                }
            }

            return false;
        }

        private async Task HandleSessionMessageAsync(string clientId, string source, byte[] sessionId, byte[] data)
        {
            var remote = Address.RemoveIdentifier(source);
            var remoteAddress = remote.Address;
            var remoteClientId = remote.ClientId;
            var sessionKey = Session.MakeKey(remoteAddress, sessionId.ToHexString());

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
                Address.AddIdentifierPrefix(destination, clientId),
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
                Address.AddIdentifierPrefix(destination, clientId),
                data,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClientAsync<T>(
            string clientId,
            IList<string> destinations,
            byte[] data,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
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
                destinations.Select(x => Address.AddIdentifierPrefix(x, clientId)).ToList(),
                data,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }

        private async Task<byte[]> SendWithClientAsync<T>(
            string clientId,
            IList<string> destinations,
            string text,
            SendOptions options,
            Channel<T> responseChannel = null,
            Channel<T> timeoutChannel = null)
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
                destinations.Select(x => Address.AddIdentifierPrefix(x, clientId)).ToList(),
                text,
                options,
                responseChannel,
                timeoutChannel);

            return messageId;
        }
    }
}
