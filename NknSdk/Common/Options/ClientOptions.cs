namespace NknSdk.Common.Options
{
    public class ClientOptions
    {
        public ClientOptions()
        {
            this.ReconnectIntervalMin = 1_000;
            this.ReconnectIntervalMax = 64_000;
            this.ResponseTimeout = 5_000;
            this.MessageHoldingSeconds = 0;
            this.Encrypt = true;
            this.RpcServerAddress = Constants.RpcServerAddress;
            this.UseTls = true;
        }

        public string SeedHex { get; set; }

        public string Identifier { get; set; }

        public int? ReconnectIntervalMin { get; set; }

        public int? ReconnectIntervalMax { get; set; }

        public int? ResponseTimeout { get; set; }

        public int? MessageHoldingSeconds { get; set; }

        public bool? Encrypt { get; set; }

        public string RpcServerAddress { get; set; }

        public bool? UseTls { get; set; }
    }
}
