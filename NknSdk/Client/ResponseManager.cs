using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using NknSdk.Common;
using NknSdk.Common.Extensions;
using NknSdk.Common.Protobuf.Payloads;

namespace NknSdk.Client
{
    public class ResponseManager<T>
    {
        private readonly IDictionary<string, ResponseProcessor<T>> responseProcessors;

        private Timer checkTimeoutTimer;

        public ResponseManager()
        {
            this.responseProcessors = new ConcurrentDictionary<string, ResponseProcessor<T>>();
            
            this.checkTimeoutTimer = new Timer(
                state => this.CheckTimeout(), 
                null, 
                Constants.CheckTimeoutInterval, 
                Timeout.Infinite);
        }

        public void Add(ResponseProcessor<T> processor)
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
            this.checkTimeoutTimer.Dispose();
            this.Clear();
        }

        public void ProcessResponse(string messageId, object responseData, PayloadType payloadType)
        {
            if (this.responseProcessors.ContainsKey(messageId))
            {
                var responseProcessor = this.responseProcessors[messageId];

                responseProcessor.HandleResponse(responseData);

                this.responseProcessors.Remove(messageId);
            }
        }

        public void ProcessResponse(byte[] messageId, object data, PayloadType payloadType)
        {
            this.ProcessResponse(messageId.ToHexString(), data, payloadType);
        }

        private void CheckTimeout()
        {
            var now = DateTime.Now;
            var expiredProcessors = this.responseProcessors.Values.Where(x => x.HasExpired(now));

            foreach (var expiredProcessor in expiredProcessors)
            {
                expiredProcessor.HandleTimeout();

                this.responseProcessors.Remove(expiredProcessor.MessageId);
            }

            this.checkTimeoutTimer?.Dispose();

            this.checkTimeoutTimer = new Timer(
                state => this.CheckTimeout(),
                null,
                Constants.CheckTimeoutInterval,
                Timeout.Infinite);
        }
    }
}