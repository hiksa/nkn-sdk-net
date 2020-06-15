namespace Ncp
{
    public class SessionOptions
    {
        public SessionOptions()
        {
            this.CheckBytesReadInterval = Constants.DefaultCheckBytesReadInterval;
            this.CheckTimeoutInterval = Constants.DefaultCheckTimeoutInterval;
            this.FlushInterval = Constants.DefaultFlushInterval;
            this.InitialConnectionWindowSize = Constants.DefaultInitialConnectionWindowSize;
            this.InitialRetransmissionTimeout = Constants.DefaultInitialRetransmissionTimeout;
            this.Linger = Constants.DefaultLinger;
            this.MaxAckSeqListSize = Constants.DefaultMaxAckSeqListSize;
            this.MaxConnectionWindowSize = Constants.DefaultMaxConnectionWindowSize;
            this.MaxRetransmissionTimeout = Constants.DefaultMaxRetransmissionTimeout;
            this.MinConnectionWindowSize = Constants.DefaultMinConnectionWindowSize;
            this.Mtu = Constants.DefaultMtu;
            this.NonStream = Constants.DefaultNonStream;
            this.SendAckInterval = Constants.DefaultSendAckInterval;
            this.SendBytesReadThreshold = Constants.DefaultSendBytesReadThreshold;
            this.SessionWindowSize = (int)Constants.DefaultSessionWindowSize;
        }

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

        public SessionOptions WithMtu(int mtu)
        {
            this.Mtu = mtu;
            return this;
        }

        public SessionOptions WithInitialConnectionWindowSize(int size)
        {
            this.InitialConnectionWindowSize = size;
            return this;
        }

        public SessionOptions WithMinConnectionWindowSize(int size)
        {
            this.MinConnectionWindowSize = size;
            return this;
        }

        public SessionOptions WithMaxAckSeqListSize(int size)
        {
            this.MaxAckSeqListSize = size;
            return this;
        }
    }
}
