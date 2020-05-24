using ProtoBuf;
using System;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    [Serializable]
    public class TransferAsset : ISerializable
    {
        [ProtoMember(1, IsPacked = true)]
        public byte[] Sender { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Recipient { get; set; }

        [ProtoMember(3)]
        public long Amount { get; set; }
    }
}
