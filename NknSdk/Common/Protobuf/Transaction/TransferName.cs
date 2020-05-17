using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class TransferName
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2)]
        public byte[] Registrant { get; set; }

        [ProtoMember(3)]
        public byte[] Recipient { get; set; }
    }
}
