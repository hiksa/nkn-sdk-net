namespace NknSdk.Common.Rpc.Results
{
    public class GetNonceByAddrResult
    {
        public ulong? Nonce { get; set; }

        public ulong? NonceInTxPool { get; set; }
    }
}
