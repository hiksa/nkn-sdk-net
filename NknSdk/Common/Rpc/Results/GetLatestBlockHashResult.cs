using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetLatestBlockHashResult
    {
        public string Hash { get; set; }

        public long Height { get; set; }
    }
}
