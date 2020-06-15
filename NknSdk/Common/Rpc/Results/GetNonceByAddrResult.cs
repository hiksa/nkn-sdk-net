namespace NknSdk.Common.Rpc.Results
{
    public class GetNonceByAddrResult
    {
        public long? Nonce { get; set; }

        public long? NonceInTxPool { get; set; }
    }
}
