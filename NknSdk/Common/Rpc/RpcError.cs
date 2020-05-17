namespace NknSdk.Common.Rpc
{
    public class RpcError
    {
        public int Code { get; set; }

        public string Data { get; set; }

        public string Message { get; set; }

        public override string ToString() => this.Data;
    }
}
