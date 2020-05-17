using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetSubscribersResult
    {
        public IEnumerable<object> Subscribers { get; set; }
    }
}
