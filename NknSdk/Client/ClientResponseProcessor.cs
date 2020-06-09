using System;
using System.Threading.Channels;

using NknSdk.Common.Extensions;

namespace NknSdk.Client
{
    public class ClientResponseProcessor<T>
    {
        private readonly DateTime? deadline;

        private readonly Channel<T> responseChannel;

        public ClientResponseProcessor(
            byte[] messageId,
            int? timeout,
            Channel<T> responseChannel)
            : this(messageId.ToHexString(), timeout, responseChannel) { }

        public ClientResponseProcessor(
            string messageId, 
            int? timeout,
            Channel<T> responseChannel)
        {
            this.MessageId = messageId;

            if (timeout != null)
            {
                this.deadline = DateTime.Now.AddMilliseconds(timeout.Value);
            }

            this.responseChannel = responseChannel;
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
        }

        public void HandleTimeout()
        {
            //this.timeoutHandler?.Invoke("Message timeout");
        }
    }
}
