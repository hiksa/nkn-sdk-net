﻿using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class Subscribe
    {
        [ProtoMember(1)]
        public byte[] Subscriber { get; set; }

        [ProtoMember(2)]
        public string Identifier { get; set; }

        [ProtoMember(3)]
        public string Topic { get; set; }

        [ProtoMember(4)]
        public int Bucket { get; set; }

        [ProtoMember(5)]
        public int Duration { get; set; }

        [ProtoMember(6)]
        public string Meta { get; set; }
    }
}