using Ncp;
using NknSdk.Client;
using NknSdk.Client.Model;
using NknSdk.Common;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NknSdk.Client.Handlers;
using System.Runtime.Caching;
using NknSdk.Common.Protobuf.Payloads;
using System.Text.RegularExpressions;
using System.Threading;

namespace NknSdk.MultiClient
{
    public class MultiClient
    {
        public event EventHandler Connected;
        public event TextMessageHandler TextReceived;
        public event DataMessageHandler DataReceived;
        public event SessionMessageHandler SessionReceived;

        private readonly MultiClientOptions options;
        private readonly IDictionary<string, Client.Client> clients;
        private readonly Client.Client defaultClient;
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

            var clients = new Dictionary<string, Client.Client>();
            if (options.OriginalClient)
            {
                var clientId = Utils.AddIdentifier("", "");
                clients[clientId] = new Client.Client(options);

                if (string.IsNullOrWhiteSpace(options.Seed))
                {
                    options.Seed = clients[clientId].Key.Seed;
                }
            }

            for (int i = 0; i < options.NumberOfSubClients; i++)
            {
                var clientId = Utils.AddIdentifier("", i.ToString());
                options.Identifier = Utils.AddIdentifier(baseIdentifier, i.ToString());
                clients[clientId] = new Client.Client(options);

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
                throw new Exception();
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
            this.isReady = false;
            this.isClosed = false;
            this.messageCache = new MemoryCache("messageCache");

            foreach (var clientId in clientIds)
            {
                clients[clientId].OnMessage(async request =>
                {
                    if (this.isClosed)
                    {
                        return false;
                    }

                    if (request.PayloadType == PayloadType.Session)
                    {
                        if (!request.IsEncrypted)
                        {
                            return false;
                        }

                        try
                        {
                            await this.HandleSessionMessageAsync(clientId, request.Source, request.MessageId, request.Payload); ;
                        }
                        catch (Exception)
                        {
                            throw new Exception();
                        }

                        return false;
                    }

                    var key = request.MessageId.ToHexString();

                    if (this.messageCache.Get(key) != null)
                    {
                        return false;
                    }

                    var expiration = DateTime.Now.AddSeconds(options.MessageHoldingSeconds.Value);
                    this.messageCache.Set(key, clientId, expiration);

                    var removeIdentifierResult = Utils.RemoveIdentifier(request.Source);
                    request.Source = removeIdentifierResult.Address;

                    var responses = Enumerable.Empty<object>();

                    if (this.messageListeners.Any())
                    {
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
                    }

                    if (request.NoReply == false)
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
                                        request.Source,
                                        bytes,
                                        new SendOptions
                                        {
                                            IsEncrypted = request.IsEncrypted,
                                            HoldingSeconds = 0,
                                            ReplyToId = request.MessageId.ToHexString()
                                        });

                                    responded = true;
                                }
                                else if (response is string text)
                                {



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
                                        Utils.AddIdentifierPrefix(request.Source, id),
                                        request.MessageId,
                                        request.IsEncrypted);
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
                throw new Exception();
            }

            destination = await this.defaultClient.ProcessDestination(destination);

            try
            {
                var tasks = readyClientIds.Select(x => this.SendWithClient(x, destination, data, options, responseCallback, timeoutCallback));
                return await Task.WhenAny(tasks);
            }
            catch (Exception)
            {
                throw new Exception();
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
                throw new Exception();
            }

            if (client.IsReady == false)
            {
                throw new Exception();
            }

            return client.Send(
                Utils.AddIdentifierPrefix(destination, clientId),
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

        private IEnumerable<string> GetReadyClientIds()
            => this.clients.Keys.Where(x => this.clients.ContainsKey(x) && this.clients[x].IsReady);

        private bool ShouldAcceptAddress(string address)
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
            var remote = Utils.RemoveIdentifier(source);
            var remoteAddress = remote.Address;
            var remoteClientId = remote.ClientId;
            var sessionKey = Utils.MakeSessionKey(remoteAddress, sessionId.ToHexString());

            Session session;

            var existed = this.sessions.ContainsKey(sessionKey);
            if (existed)
            {
                session = this.sessions[sessionKey];
            }
            else
            {
                if (this.ShouldAcceptAddress(remoteAddress) == false)
                {
                    throw new Exception();
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
                    await Task.WhenAll(this.sessionListeners.Select(async x =>
                    {
                        try
                        {
                            return await x(session);
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
                        throw new Exception();
                    }

                    var payload = MessageFactory.MakeSessionPayload(data, sessionId);
                    var destination = Utils.AddIdentifierPrefix(remoteAddress, remoteClientId);

                    await client.Send(destination, payload);
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
                addresses = new Regex[] { MultiClientConstants.DefaultSessionAllowedAddressRegex };
            }

            this.acceptedAddresses = addresses;
        }

        public async Task<Session> Dial(string remoteAddress, SessionConfiguration sessionConfiguration)
        {
            var dialTimeout = Constants.DefaultInitialRetransmissionTimeout;

            var sessionId = PseudoRandom.RandomBytesAsHexString(MultiClientConstants.SessionIdSize);

            var sessionKey = Utils.MakeSessionKey(remoteAddress, sessionId);

            var session = this.MakeSession(remoteAddress, sessionId, sessionConfiguration);

            this.sessions.Add(sessionKey, session);

            await session.DialAsync(dialTimeout);

            return session;
        }
    }
}
