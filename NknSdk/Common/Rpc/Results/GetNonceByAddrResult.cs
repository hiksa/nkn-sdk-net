using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetNonceByAddrResult
    {
        public ulong? Nonce { get; set; }

        public ulong? NonceInTxPool { get; set; }
    }
}
