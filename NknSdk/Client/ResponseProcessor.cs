using System;
using System.Threading.Channels;

using NknSdk.Common.Extensions;

namespace NknSdk.Client
{
    public class ResponseProcessor<T>
    {
        private readonly DateTime? deadline;
        private readonly Channel<T> responseChannel;
        private readonly Channel<T> timeoutChannel;

        public ResponseProcessor(
            byte[] messageId,
            int? timeout,
            Channel<T> responseChannel,
            Channel<T> timeoutChannel)
            : this(messageId.ToHexString(), timeout, responseChannel, timeoutChannel) 
        { }

        public ResponseProcessor(
            string messageId, 
            int? timeout,
            Channel<T> responseChannel,
            Channel<T> timeoutChannel)
        {
            this.MessageId = messageId;

            if (timeout != null)
            {
                this.deadline = DateTime.Now.AddMilliseconds(timeout.Value);
            }

            this.responseChannel = responseChannel;
            this.timeoutChannel = timeoutChannel;
        }

        public string MessageId { get; }

        public bool HasExpired(DateTime? currentTime)
        {
            if (this.deadline == null)
            {
                return false;
            }

            if (currentTime == null)
            {
                currentTime = DateTime.Now;
            }

            return currentTime > this.deadline;
        }

        public void HandleResponse(object responseData)
        {
            this.responseChannel.Writer.TryWrite((T)responseData);
            this.responseChannel.Writer.TryComplete();
        }

        public void HandleTimeout()
        {
            this.timeoutChannel.Writer.TryWrite(default);
            this.timeoutChannel.Writer.TryComplete();
        }
    }
}
