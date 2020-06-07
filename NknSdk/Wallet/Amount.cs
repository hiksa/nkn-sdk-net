using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Wallet
{
    public class Amount
    {
        private const int Size = 100_000_000;

        private readonly decimal amount;

        public Amount(decimal amount)
        {
            this.amount = amount;
        }

        public long Value => (long)Math.Floor(this.amount * Size);
    }
}
