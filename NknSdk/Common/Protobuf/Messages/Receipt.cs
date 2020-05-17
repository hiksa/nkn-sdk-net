using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class Receipt : ISerializable
    {
        [ProtoMember(1)]
        public byte[] PreviousSignature { get; set; }

        [ProtoMember(2)]
        public byte[] Signature { get; set; }
    }
}
