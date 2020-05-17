using Ncp;
using NknSdk.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.MultiClient
{
    public class MultiClientOptions : ClientOptions
    {
        public int NumberOfSubClients { get; set; }

        public bool OriginalClient { get; set; }

        public int MessageCacheExpiration { get; set; }

        public SessionConfiguration SessionConfiguration { get; set; }

        public static new MultiClientOptions Default { get; } = new MultiClientOptions
        {
            ReconnectIntervalMin = 1_000,
            ReconnectIntervalMax = 64_000,
            ResponseTimeout = 50_000,
            MessageHoldingSeconds = 0,
            Encrypt = true,
            RpcServerAddress = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet",
            UseTls = true,
            SessionConfiguration = SessionConfiguration.Default
        };        
    }
}
