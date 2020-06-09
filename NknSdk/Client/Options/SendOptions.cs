﻿using NknSdk.Common;

namespace NknSdk.Client
{
    public class SendOptions
    {
        public SendOptions()
        {
            this.IsEncrypted = true;
            this.MessageId = PseudoRandom.RandomBytesAsHexString(Constants.MessageIdLength);
        }

        public int? ResponseTimeout { get; set; }

        public bool? IsEncrypted { get; set; }

        public int? HoldingSeconds { get; set; }

        public bool? NoReply { get; set; }

        public string MessageId { get; set; }

        public string ReplyToId { get; set; }
    }
}