namespace Ncp
{
    public static class Constants
    {
        public const int MinSequenceId = 1;
        public const int DefaultMtu = 1024;
        public const int DefaultInitialConnectionWindowSize = 32;
        public const int DefaultMaxConnectionWindowSize = 256;
        public const int DefaultMinConnectionWindowSize = 1;
        public const int DefaultMaxAckSeqListSize = 32;
        public const int DefaultFlushInterval = 10;
        public const int DefaultLinger = 1_000;
        public const int DefaultInitialRetransmissionTimeout = 50_000;
        public const int DefaultMaxRetransmissionTimeout = 100_000;
        public const int DefaultSendAckInterval = 50;
        public const int DefaultCheckTimeoutInterval = 500;
        public const int DefaultCheckBytesReadInterval = 1000;
        public const int DefaultSendBytesReadThreshold = 200;
        public const long DefaultSessionWindowSize = 4 << 20;
        public const bool DefaultNonStream = false;

        public const int MaximumWaitTime = 1000;
    }
}
