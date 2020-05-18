using ProtoBuf;
using System.Collections.Generic;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Transaction : ISerializable
    {
        [ProtoMember(1)]
        public UnsignedTransaction UnsignedTransaction { get; set; }

        [ProtoMember(2)]
        public IList<Program> Programs { get; set; }

        public string Hash { get; set; }
    }
}
