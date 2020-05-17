namespace Ncp
{
    public class SessionConfiguration
    {
        static SessionConfiguration()
        {
            Default = CreateDefault();
        }

        private SessionConfiguration() { }

        public static SessionConfiguration Default { get; }

        public int Mtu { get; set; }

        public int InitialConnectionWindowSize { get; set; }

        public int MaxConnectionWindowSize { get; set; }

        public int MinConnectionWindowSize { get; set; }

        public int MaxAckSeqListSize { get; set; }

        public int FlushInterval { get; set; }

        public int Linger { get; set; }

        public int InitialRetransmissionTimeout { get; set; }

        public int MaxRetransmissionTimeout { get; set; }

        public int SendAckInterval { get; set; }

        public int CheckTimeoutInterval { get; set; }

        public int CheckBytesReadInterval { get; set; }

        public int SendBytesReadThreshold { get; set; }

        public int SessionWindowSize { get; set; }

        public bool NonStream { get; set; }

        private static SessionConfiguration CreateDefault()
            => new SessionConfiguration
            {
                CheckBytesReadInterval = Constants.DefaultCheckBytesReadInterval,
                CheckTimeoutInterval = Constants.DefaultCheckTimeoutInterval,
                FlushInterval = Constants.DefaultFlushInterval,
                InitialConnectionWindowSize = Constants.DefaultInitialConnectionWindowSize,
                InitialRetransmissionTimeout = Constants.DefaultInitialRetransmissionTimeout,
                Linger = Constants.DefaultLinger,
                MaxAckSeqListSize = Constants.DefaultMaxAckSeqListSize,
                MaxConnectionWindowSize = Constants.DefaultMaxConnectionWindowSize,
                MaxRetransmissionTimeout = Constants.DefaultMaxRetransmissionTimeout,
                MinConnectionWindowSize = Constants.DefaultMinConnectionWindowSize,
                Mtu = Constants.DefaultMtu,
                NonStream = Constants.DefaultNonStream,
                SendAckInterval = Constants.DefaultSendAckInterval,
                SendBytesReadThreshold = Constants.DefaultSendBytesReadThreshold,
                SessionWindowSize = (int)Constants.DefaultSessionWindowSize
            };

        public SessionConfiguration WithMtu(int mtu)
        {
            this.Mtu = mtu;
            return this;
        }

        public SessionConfiguration WithInitialConnectionWindowSize(int size)
        {
            this.InitialConnectionWindowSize = size;
            return this;
        }

        public SessionConfiguration WithMinConnectionWindowSize(int size)
        {
            this.MinConnectionWindowSize = size;
            return this;
        }

        public SessionConfiguration WithMaxAckSeqListSize(int size)
        {
            this.MaxAckSeqListSize = size;
            return this;
        }
    }
}
