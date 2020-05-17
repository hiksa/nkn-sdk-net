using ProtoBuf;
using System;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    [Serializable]
    public class TransferAsset
    {
        [ProtoMember(1)]
        public byte[] Sender { get; set; }

        [ProtoMember(2)]
        public byte[] Recipient { get; set; }

        [ProtoMember(3)]
        public long Amount { get; set; }
    }
}
