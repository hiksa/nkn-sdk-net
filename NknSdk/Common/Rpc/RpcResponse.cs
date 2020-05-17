namespace NknSdk.Common.Rpc
{
    public class RpcResponse<T>
    {
        public string Id { get; set; }

        public string Jsonrpc { get; set; }

        public T Result { get; set; }

        public RpcError Error { get; set; }

        public bool IsSuccess => this.Error == null;
    }
}
