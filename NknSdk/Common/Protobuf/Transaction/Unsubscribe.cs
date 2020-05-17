using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Unsubscribe
    {
        [ProtoMember(1)]
        public byte[] Subscriber { get; set; }

        [ProtoMember(2)]
        public string Identifier { get; set; }

        [ProtoMember(3)]
        public string Topic { get; set; }
    }
}
