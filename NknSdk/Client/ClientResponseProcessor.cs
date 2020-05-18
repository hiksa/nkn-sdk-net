using System;
using NknSdk.Common;
using static NknSdk.Client.Handlers;

namespace NknSdk.Client
{
    public class ClientResponseProcessor
    {
        private readonly DateTime? deadline;

        private readonly ResponseHandler responseHandler;
        private readonly TimeoutHandler timeoutHandler;

        public ClientResponseProcessor(
            byte[] messageId,
            int? timeout,
            ResponseHandler responseHandler,
            TimeoutHandler timeoutHandler)
            : this(messageId.ToHexString(), timeout, responseHandler, timeoutHandler) { }

        public ClientResponseProcessor(
            string messageId, 
            int? timeout, 
            ResponseHandler responseHandler, 
            TimeoutHandler timeoutHandler)
        {
            this.MessageId = messageId;

            if (timeout != null)
            {
                this.deadline = DateTime.Now.AddMilliseconds(timeout.Value);
            }

            this.responseHandler = responseHandler;
            this.timeoutHandler = timeoutHandler;
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
            this.responseHandler?.Invoke(data);
        }

        public void HandleTimeout()
        {
            this.timeoutHandler?.Invoke("Message timeout");
        }
    }
}
