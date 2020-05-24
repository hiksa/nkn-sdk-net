using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Messages
{
    [ProtoContract]
    public class ClientMessage : ISerializable
    {
        [ProtoMember(1)]
        public ClientMessageType? Type { get; set; }

        [ProtoMember(2, IsPacked = true)]
        public byte[] Message { get; set; }

        [ProtoMember(3)]
        public CompressionType? CompressionType { get; set; }
    }
}
