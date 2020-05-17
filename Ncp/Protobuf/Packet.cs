using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ncp.Protobuf
{
    [ProtoContract]
    public class Packet
    {
        [ProtoMember(1)]
        public int SequenceId { get; set; }

        [ProtoMember(2)]
        public byte[] Data { get; set; }

        [ProtoMember(3)]
        public int[] AckStartSeq { get; set; }

        [ProtoMember(4)]
        public int[] AckSeqCount { get; set; }

        [ProtoMember(5)]
        public long BytesRead { get; set; }

        [ProtoMember(6)]
        public string[] ClientIds { get; set; }

        [ProtoMember(7)]
        public int WindowSize { get; set; }

        [ProtoMember(8)]
        public int Mtu { get; set; }

        [ProtoMember(9)]
        public bool IsClosed { get; set; }

        [ProtoMember(10)]
        public bool IsHandshake { get; set; }
    }
}
