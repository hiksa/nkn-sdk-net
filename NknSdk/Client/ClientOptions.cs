namespace NknSdk.Client
{
    public class ClientOptions
    {
        public string Seed { get; set; }

        public string Identifier { get; set; }

        public int? ReconnectIntervalMin { get; set; }

        public int? ReconnectIntervalMax { get; set; }

        public int? ResponseTimeout { get; set; }

        public int? MessageHoldingSeconds { get; set; }

        public bool? Encrypt { get; set; }

        public string RpcServerAddress { get; set; }

        public bool? UseTls { get; set; }

        public static ClientOptions Default { get; } = new ClientOptions
        {
            ReconnectIntervalMin = 1_000,
            ReconnectIntervalMax = 64_000,
            ResponseTimeout = 5_000,
            MessageHoldingSeconds = 0,
            Encrypt = true,
            RpcServerAddress = "https://mainnet-rpc-node-0001.nkn.org/mainnet/api/wallet",
            UseTls = true
        };
    }
}
