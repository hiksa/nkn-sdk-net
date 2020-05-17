using NknSdk.Common;
using NknSdk.Common.Protobuf.Payloads;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NknSdk.Client
{
    public class ResponseManager
    {
        private readonly IDictionary<string, ResponseProcessor> responseProcessors;
        // TODO: add timer

        public ResponseManager()
        {
            this.responseProcessors = new Dictionary<string, ResponseProcessor>();
        }

        public void Add(ResponseProcessor processor)
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
            // TODO: clear timer
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
            var expiredProcessors = new List<ResponseProcessor>();
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

            // TODO: Set timer
        }
    }
}