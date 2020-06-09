using System;
using System.Threading.Channels;

using NknSdk.Common.Extensions;

namespace NknSdk.Client.Network
{
    public class ClientResponseProcessor<T>
    {
        private readonly DateTime? deadline;

        private readonly Channel<T> responseChannel;
        private readonly Channel<T> timeoutChannel;

        public ClientResponseProcessor(
            byte[] messageId,
            int? timeout,
            Channel<T> responseChannel,
            Channel<T> timeoutChannel)
            : this(messageId.ToHexString(), timeout, responseChannel, timeoutChannel) { }

        public ClientResponseProcessor(
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

        public void HandleResponse(object data)
        {
            this.responseChannel.Writer.WriteAsync((T)data);
            this.responseChannel.Writer.Complete();
        }

        public void HandleTimeout()
        {
            this.timeoutChannel.Writer.WriteAsync(default);
            this.timeoutChannel.Writer.Complete();
        }
    }
}
