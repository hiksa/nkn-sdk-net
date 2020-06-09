using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetSubscribersWithMetadataResult
    {
        public Dictionary<string, string> Subscribers { get; set; }

        public Dictionary<string, string> SubscribersInTxPool { get; set; }
    }
}
