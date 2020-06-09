using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetBalanceResult
    {
        public decimal Amount { get; set; }

        public string Address { get; set; }
    }
}
