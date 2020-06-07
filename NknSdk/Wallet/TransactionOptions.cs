using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Wallet
{
    public class TransactionOptions
    {
        public long? Fee { get; set; }

        public string Attributes { get; set; }

        public long Nonce { get; set; }
    }
}
