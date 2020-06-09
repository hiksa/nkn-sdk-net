using System.Collections.Generic;

namespace NknSdk.Common.Rpc.Results
{
    public class GetSubscribersResult
    {
        public IEnumerable<string> Subscribers { get; set; }

        public IEnumerable<string> SubscribersInTxPool { get; set; }
    }
}
