using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NknSdk.Common.Extensions;
using NknSdk.Common.Protobuf.Payloads;

namespace NknSdk.Client.Network
{
    public class ClientResponseManager<T>
    {
        private readonly IDictionary<string, ClientResponseProcessor<T>> responseProcessors;
        private Timer timer;

        public ClientResponseManager()
        {
            this.responseProcessors = new ConcurrentDictionary<string, ClientResponseProcessor<T>>();

            this.CheckTimeout();
        }

        public void Add(ClientResponseProcessor<T> processor)
        {
            this.responseProcessors.Add(processor.MessageId, processor);
        }

        public void Clear()
        {
            foreach (var processor in this.responseProcessors.Values)
            {
                processor.HandleTimeout();
            }

            this.responseProcessors.Clear();
        }

        public void Stop()
        {
            this.timer.Dispose();
            this.Clear();
        }

        public void Respond(string messageId, object data, PayloadType payloadType)
        {
            if (this.responseProcessors.ContainsKey(messageId))
            {
                var responseProcessor = this.responseProcessors[messageId];

                responseProcessor.HandleResponse(data);

                this.responseProcessors.Remove(messageId);
            }
        }

        public void Respond(byte[] messageId, object data, PayloadType payloadType)
        {
            this.Respond(messageId.ToHexString(), data, payloadType);
        }

        public void CheckTimeout()
        {
            var expiredProcessors = new List<ClientResponseProcessor<T>>();
            var now = DateTime.Now;

            foreach (var processor in this.responseProcessors.Values)
            {
                if (processor.HasExpired(now))
                {
                    expiredProcessors.Add(processor);
                }
            }

            foreach (var expiredProcessor in expiredProcessors)
            {
                expiredProcessor.HandleTimeout();
                this.responseProcessors.Remove(expiredProcessor.MessageId);
            }

            this.timer = new Timer(state => this.CheckTimeout(), new { }, Constants.CheckTimeoutInterval, Timeout.Infinite);
        }
    }
}