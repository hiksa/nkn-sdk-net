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

namespace NknSdk.Client
{
    public class MultiClient
    {
        public event EventHandler Connected;
        public event TextMessageHandler TextReceived;
        public event DataMessageHandler DataReceived;
        public event SessionMessageHandler SessionReceived;

        private readonly MultiClientOptions options;
        private readonly IDictionary<string, Client> clients;
        private readonly Client defaultClient;
        private readonly CryptoKey key;
        private readonly string identifier;
        private readonly string address;
        private IEnumerable<Regex> acceptedAddresses;
        private readonly Dictionary<string, Session> sessions;
        private readonly bool isReady;
        private bool isClosed;
        private MemoryCache messageCache;
        public IList<Func<MessageHandlerRequest, Task<object>>> connectListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
        public IList<Func<MessageHandlerRequest, Task<object>>> messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
        public IList<Func<Session, Task<object>>> sessionListeners = new List<Func<Session, Task<object>>>();

        public MultiClient(MultiClientOptions options)
        {
            var baseIdentifier = options.Identifier ?? "";

            var clients = new Dictionary<string, Client>();
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
                options.Identifier = Client.AddIdentifier(baseIdentifier, i.ToString());
                clients[clientId] = new Client(options);

                clients[clientId].Connected += (object sender, EventArgs args) => this.Connected?.Invoke(this, null);

                if (i == 0 && string.IsNullOrWhiteSpace(options.Seed))
                {
                    options.Seed = clients[clientId].Key.Seed;
                }
            }

            options.Identifier = baseIdentifier;

            var clientIds = clients.Keys.OrderBy(x => x);
            if (clientIds.Count() == 0)
            {
                throw new InvalidArgumentException("should have at least one client");
            }

            this.options = options;
            this.clients = clients;
            this.defaultClient = clients[clientIds.First()];
            this.key = this.defaultClient.Key;
            this.identifier = baseIdentifier;
            this.address = (string.IsNullOrWhiteSpace(baseIdentifier) ? "" : baseIdentifier + ".") + this.key.PublicKey;
            
            this.connectListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.messageListeners = new List<Func<MessageHandlerRequest, Task<object>>>();
            this.sessionListeners = new List<Func<Session, Task<object>>>();
            this.acceptedAddresses = new List<Regex>();
            this.sessions = new Dictionary<string, Session>();
            this.messageCache = new MemoryCache("messageCache");

            this.isReady = false;
            this.isClosed = false;

            foreach (var clientId in clientIds)
            {
                clients[clientId].OnMessage(async message =>
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
                            await this.HandleSessionMessageAsync(clientId, message.Source, message.MessageId, message.Payload); ;
                        }
                        catch (AddressNotAllowedException)
                        {
                        }
                        catch (SessionClosedException)
                        {
                        }

                        return false;
                    }

                    var key = message.MessageId.ToHexString();

                    if (this.messageCache.Get(key) != null)
                    {
                        return false;
                    }

                    var expiration = DateTime.Now.AddSeconds(options.MessageHoldingSeconds.Value);
                    this.messageCache.Set(key, clientId, expiration);

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
                                    await this.Send(
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

                                    // TODO: ..

                                    responded = true;
                                }
                            }
                        }

                        if (responded == false)
                        {
                            foreach (var id in clientIds)
                            {
                                if (this.clients[id].IsReady)
                                {
                                    this.clients[id].SendAck(
                                        Client.AddIdentifierPrefix(message.Source, id),
                                        message.MessageId,
                                        message.IsEncrypted);
                                }
                            }
                        }
                    }

                    return false;
                });
            }
        }

        public async Task<Task<byte[]>> Send(
            string destination, 
            byte[] data, 
            SendOptions options = null,
            ResponseHandler responseCallback = null,
            TimeoutHandler timeoutCallback = null)
        {
            var readyClientIds = this.GetReadyClientIds();
            if (readyClientIds.Count() == 0)
            {
                throw new ClientNotReadyException();
            }

            destination = await this.defaultClient.ProcessDestinationAsync(destination);

            try
            {
                var tasks = readyClientIds.Select(x => this.SendWithClient(x, destination, data, options, responseCallback, timeoutCallback));
                return await Task.WhenAny(tasks);
            }
            catch (Exception)
            {
                throw new ApplicationException("failed to send with any client");
            }
        }

        public Task<byte[]> SendWithClient(
            string clientId, 
            string destination, 
            byte[] data, 
            SendOptions options,
            ResponseHandler responseCallback = null,
            TimeoutHandler timeoutCallback = null)
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

            return client.SendDataAsync(
                Client.AddIdentifierPrefix(destination, clientId),
                data,
                options,
                responseCallback,
                timeoutCallback);
        }

        public void OnMessage(Func<MessageHandlerRequest, Task<object>> func)
        {
            this.messageListeners.Add(func);
        }

        public void OnSession(Func<Session, Task<object>> func)
        {
            this.sessionListeners.Add(func);
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

            var existed = this.sessions.ContainsKey(sessionKey);
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

            await session.ReceiveWithAsync(clientId, remoteClientId, data);

            if (existed == false)
            {
                await session.AcceptAsync();

                if (this.sessionListeners.Count > 0)
                {
                    await Task.WhenAll(this.sessionListeners.Select(async func =>
                    {
                        try
                        {
                            return await func(session);
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }));
                }
            }
        }

        private async Task CloseAsync()
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
                    await this.clients[clientId].CloseAsync();
                }
                catch (Exception)
                {
                }

                this.messageCache = new MemoryCache("messageCache");
                this.isClosed = true;
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

        private void MultiClient_DataReceived(string sender, byte[] data)
        {
            throw new NotImplementedException();
        }

        public void Listen(IEnumerable<string> addresses)
        {
            this.Listen(addresses.Select(x => new Regex(x)));
        }

        public void Listen(IEnumerable<Regex> addresses)
        {
            if (addresses == null)
            {
                addresses = new Regex[] { Constants.DefaultSessionAllowedAddressRegex };
            }

            this.acceptedAddresses = addresses;
        }

        public async Task<Session> DialAsync(string remoteAddress, SessionConfiguration sessionConfiguration)
        {
            var dialTimeout = Ncp.Constants.DefaultInitialRetransmissionTimeout;

            var sessionId = PseudoRandom.RandomBytesAsHexString(Constants.SessionIdSize);

            var sessionKey = Session.GetKey(remoteAddress, sessionId);

            var session = this.MakeSession(remoteAddress, sessionId, sessionConfiguration);

            this.sessions.Add(sessionKey, session);

            await session.DialAsync(dialTimeout);

            return session;
        }

        private IEnumerable<string> GetReadyClientIds()
            => this.clients.Keys.Where(x => this.clients.ContainsKey(x) && this.clients[x].IsReady);
    }
}
