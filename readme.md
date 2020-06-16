# nkn-sdk-net

_This is still an experimental implementation, not intended for production use. There might be bugs!_

![nkn](https://github.com/nknorg/nkn-sdk-js/raw/master/logo.png)

Unofficial C# implementation of NKN client and wallet SDK. This implementation is a port of [nkn-sdk-js](https://github.com/nknorg/nkn-sdk-js) The SDK consists of the following main components:

- [NKN Client](#client): Send and receive data for free between any NKN clients regardless their network condition without setting up a server or relying on any third party services. Data are end to end encrypted by default. Typically you might want to use [MultiClient](#multiclient) instead of using Client directly.

- [NKN MultiClient](#multiclient): Send and receive data using multiple NKN clients concurrently to improve reliability and latency. In addition, it supports session mode, a reliable streaming protocol similar to TCP based on ncp. A C# implementation of [ncp](https://github.com/nknorg/ncp-js) is included in this sdk, but will soon be moved to a separate project.

- [NKN Wallet](#wallet): Wallet SDK for [NKN blockchain](https://github.com/nknorg/nkn). It can be used to create wallet, transfer token to NKN wallet address, register name, subscribe to topic, etc.

Advantages of using NKN Client/MultiClient for data transmission:

- Network agnostic: Neither sender nor receiver needs to have public IP address or port forwarding. NKN clients only establish outbound (websocket) connections, so Internet access is all they need. This is ideal for client side peer to peer communication.

- Top level security: All data are end to end authenticated and encrypted. No one else in the world except sender and receiver can see or modify the content of the data. The same public key is used for both routing and encryption, eliminating the possibility of man in the middle attack.

- Everything is free, open source and decentralized. (If you are curious, node relay traffic for clients for free to earn mining rewards in NKN blockchain.)

## Install / Build

Nuget package is coming in the near future. For now you can clone the repo and build with standard .net tools. Target Framework: netstandard2.1

## Client

NKN client provides the basic methods for sending and receiving data between NKN clients or topics regardless their network condition without setting up a server or relying on any third party services. Typically you might want to use [MultiClient](#multiclient) instead of using the Client class directly.

Create a client with a generated key pair:

```c#
var client = new Client();
```

Or with an identifier (used to distinguish different clients sharing the same key pair):

```c#
var client = new Client(new ClientOptions() { Identifier = "any string" });
```

Get client secret seed and public key:

```c#
Console.WriteLine(client.SeedHex);
Console.WriteLine(client.PublicKey);
```

Create a client using an existing secret seed:

```c#
var options = new ClientOptions()
{
    SeeHex = "2bc5501d131696429264eb7286c44a29dd44dd66834d9471bd8b0eb875a1edb0"
};

var client = new Client(options);
```

Secret key should be kept **SECRET**! Never put it in version control system like here.

By default the client will use bootstrap RPC server (for getting node address) provided by nkn.org. Any NKN full node can serve as a bootstrap RPC server. You can create a client using customized bootstrap RPC server:

```c#
var options = new ClientOptions()
{
    RpcServerAddress = "https://ip:port"
};

var client = new Client(options);
```

Get client NKN address, which is used to receive data from other clients:

```c#
Console.WriteLine(client.Address);
```

Listen for connection established:

```c#
client.OnConnect(() => Console.WriteLine("Client ready."));
```

Send text message to other clients and receive a text response:

```c#
var textResponse = await client.SendAsync<string>("another-client-address", "hello world!");

Console.WriteLine(textResponse.Result);
```

You can also send `byte[]`:

```c#
await client.SendAsync<byte[]>("another-client-address", new byte[] { 1, 2, 3, 4, 5 });
```

The destination address can also be a name registered using [Wallet](#wallet).

Publish text message to all subscribers of a topic (subscribing to a topic can be done through [Wallet](#wallet)):

```c#
await client.PublishAsync("topic", "hello world!");
```

Receive data from other clients:

```c#
client.OnMessage((MessageHandlerRequest request) =>
{
    Console.WriteLine("Receive message " + request.Payload, " from " + request.Source);
});
```

If a valid data (`string` or `byte[]`) is returned at the end of the handler, the data will be sent back to sender as reply:

```c#
client.OnMessage((MessageHandlerRequest request) =>
{
    return "Well received!";
});
```

Handler can also be an async method, and reply can be `byte[]` as well:

```c#
client.OnMessage(async (MessageHandlerRequest request) =>
{
    return new byte[] { 1,2,3,4,5 };
});
```

Note that if multiple message handlers are added, the result returned by the first handler (in the order of being added) will be sent as reply.

The `SendAsync` method will return a `Task` that will complete when sender receives a reply, or fail if not receiving reply or acknowledgement within the specified timeout period. Similar to message, reply can be either `byte[]`:

```c#
try
{
    var response = await client.SendAsync<byte[]>("another-client-address", "hello world!");

    Console.WriteLine("Receive a byte[] reply: " + string.Join(", ", response.Result));
}
catch (Exception e)
{
    Console.WriteLine("Catch: " + e.Message);
}
```

or `string`:

```c#
try
{
    var response = await client.SendAsync<string>("another-client-address", "hello world!");

    Console.WriteLine("Receive a string reply: " + response.Result);
}
catch (Exception e)
{
    Console.WriteLine("Catch: " + e.Message);
}
```

Client receiving data will automatically send an acknowledgement back to sender if message handler returns `null` so that sender will be able to know if the packet has been delivered. On the sender's side, it's almost the same as receiving a reply, except that the `Task` result will be `null`:

```c#
try
{
    await client.SendAsync<byte[]>("another-client-address", "hello world!");

    Console.WriteLine("Receive ACK.");
}
catch (Exception e)
{
    Console.WriteLine("Catch: " + e.Message);
}
```

If a handler returns `false`, no reply or ACK will be sent.

## MultiClient

MultiClient creates multiple NKN Client instances by adding identifier prefix (`__0__.`, `__1__.`, `__2__.`, ...) to a NKN address and send/receive packets concurrently. This will greatly increase reliability and reduce latency at the cost of more bandwidth usage (proportional to the number of clients).

MultiClient basically has the same API as [client](#client), with a few additional initial configurations and **session** mode:

```c#
var options =  new MultiClientOptions()
{
    NumberOfSubClients = 4,
    OriginalClient = false
};

var multiClient = new MultiClient(options);
```

where `OriginalClient` controls whether a client with original identifier (without adding any additional identifier prefix) will be created, and `numSubClients` controls how many sub-clients to create by adding prefix `__0__.`, `__1__.`, `__2__.`, etc. Using `OriginalClient = true` and `numSubClients: 0` is equivalent to using a standard NKN Client without any modification to the identifier. Note that if you use `OriginalClient = true` and `NumberOfSubClients` is greater than 0, your identifier should not starts with `__X__` where `X` is any number, otherwise you may end up with identifier collision.

Any additional options will be passed to NKN client.

MultiClient instance shares most of the public API as regular NKN client, see [client](#client) for usage and examples. If you need low-level property or API, you can use `multiClient.DefaultClient` to get the default client and `multiClient.Clients` to get all clients.

### Session

In addition to the default packet mode, Multiclient supports session mode - a reliable streaming protocol similar to TCP based on ncp. A c# ncp implementation is also included in this sdk, but will soon be split in a separate package.

Listen for incoming sessions (without calling `Listen()` no sessions will be accepted):

```c#
multiClient.Listen();
```

or only listen for sessions by a list of addresses:

```c#
multiClient.Listen(new string[] { "address 1", "address 2" });
```

Dial a session:

```c#
var session = await multiClient.DialAsync("another-client-address");
Console.WriteLine(session.LocalAddress + " dialed a session to " + session.RemoteAddress);
```

Accepts for incoming sessions:

```c#
multiClient.OnSession((Session session) =>
{
    Console.WriteLine(session.LocalAddress + " accepted a session from " + session.RemoteAddress);
});
```

Write to session:

```c#
await session.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });
Console.WriteLine("write success");
```

Read from session:

```c#
byte[] readResult = await session.ReadAsync();
Console.WriteLine(string.Join(", ", readResult));
```

`session.ReadAsync` also accepts a `maxSize` parameter, e.g. `session.ReadAsync(maxSize)`. If `maxSize > 0`, at most `maxSize` bytes will be returned. If `maxSize == 0` or not set, the first batch of received data will be returned. If `maxSize < 0`, all received data will be concatenated and returned together.

## Wallet

NKN Wallet SDK.

Create a new wallet with a generated key pair:

```c#
var options = new WalletOptions() { Password = "password" };
var wallet = new Wallet(options);
```

Create wallet from a secret seed:

```c#
var options = new WalletOptions()
{
    Password = "new-wallet-password",
    SeedHex = wallet.SeedHex
};

var wallet = new Wallet(options);
```

Export wallet to JSON string:

```c#
var walletJson = wallet.ToJson();
```

Load wallet from JSON and password:

```c#
var options = new WalletOptions() { Password = "password" };
var wallet = Wallet.FromJson(walletJson, options);
```

or async:

```c#
var options = new WalletOptions() { Password = "password" };
var wallet = await Wallet.FromJsonAsync(walletJson, options);
```

By default the wallet will use RPC server provided by nkn.org. Any NKN full node can serve as a RPC server. You can create a wallet using customized RPC server:

```c#
var options = new WalletOptions()
{
    Password = "new-wallet-password",
    RpcServerAddress = "https://ip:port"
};

var wallet = new Wallet(options);
```

Verify whether an address is a valid NKN wallet address:

```c#
Console.WriteLine(Wallet.VerifyAddress(wallet.Address));
```

Verify password of the wallet:

```c#
Console.WriteLine(wallet.VerifyPassword("password"));
```

or the async version:

```c#
Console.WriteLine(await wallet.VerifyPasswordAsync("password"));
```

Get balance of this wallet:

```c#
var balance = await wallet.GetBalanceAsync();
Console.WriteLine($"The balance of this wallet with address {balance.Address} is {balance.Amount}");
```

Transfer token to another wallet address:

```c#
var options = new TransactionOptions() { Fee = 0.1, Attributes = "hello world" };
var txnHash = await wallet.TransferToAsync(wallet.Address, 1, options);
Console.WriteLine("Transfer transaction hash: " + txnHash);
```

Subscribe to a topic for this wallet for next 100 blocks (around 20 seconds per block), client using the same key pair (seed) as this wallet and same identifier as passed to `SubscribAsync` will be able to receive messages from this topic:

```c#
var options = new TransactionOptions() { Fee = 0.1 };
var txnHash = await wallet.SubscribeAsync("topic", 100, "identifier", "metadata", options);
Console.WriteLine("Subscribe transaction hash: " + txnHash);
```

## Contributing

**Can I submit a bug, suggestion or feature request?**

Yes. Please open an issue for that.

**Can I contribute patches?**

Yes, your help is appreciated! To make contributions, please fork the repo, push your changes to the forked repo with signed-off commits, and open a pull request here.

Please sign off your commit. This means adding a line "Signed-off-by: Name <email>" at the end of each commit, indicating that you wrote the code and have the right to pass it on as an open source patch. This can be done automatically by adding -s when committing:

```shell
git commit -s
```

## NKN Community

- [Forum](https://forum.nkn.org/)
- [Discord](https://discord.gg/c7mTynX)
- [Telegram](https://t.me/nknorg)
- [Reddit](https://www.reddit.com/r/nknblockchain/)
- [Twitter](https://twitter.com/NKN_ORG)

_This is still an experimental implementation, not intended for production use. There might be bugs!_
