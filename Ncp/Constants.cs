using System;
using System.Threading.Channels;

namespace Ncp
{
    public static class Constants
    {
        public const int MinSequenceId = 1;
        public const int DefaultMtu = 100_024;
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
        public const int MaximumWaitTime = 1000;
        public const long DefaultSessionWindowSize = 4 << 24;
        public const bool DefaultNonStream = false;
         
        static Constants()
        {
            ClosedChannel = Channel.CreateUnbounded<uint?>();
            ClosedChannel.Writer.Complete();

            MaxWaitError = new Exception("max wait time reached");
        }

        public static Exception MaxWaitError { get; private set; }

        public static Channel<uint?> ClosedChannel { get; private set; }
    }
}
