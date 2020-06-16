using System.Threading.Tasks;

using NknSdk.Common.Options;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Rpc.Results;

namespace NknSdk.Common
{
    public interface ITransactionSender
    {
        string PublicKey { get; }

        Task<GetNonceByAddrResult> GetNonceAsync();

        Transaction CreateTransaction(Payload payload, long nonce, TransactionOptions options);

        Task<string> SendTransactionAsync(Transaction tx);
    }
}
