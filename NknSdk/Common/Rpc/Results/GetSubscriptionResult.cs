using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetSubscriptionResult
    {
        public string Meta { get; set; }

        public int ExpiresAt { get; set; }
    }
}
