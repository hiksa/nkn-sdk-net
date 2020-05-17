using System;

namespace NknSdk.Common.Protobuf.Transaction
{
    [Serializable]
    public enum PayloadType
    {
        Coinbase,
        TransferAsset,
        SignatureChainTransaction,
        RegisterName,
        TransferName,
        DeleteName,
        Subscribe,
        Unsubscribe,
        GenerateId,
        NanoPay,
        IssueAsset
    }
}
