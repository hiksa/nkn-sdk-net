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
        public uint SequenceId { get; set; }

        [ProtoMember(2)]
        public byte[] Data { get; set; }

        [ProtoMember(3, IsPacked = true)]

        public uint[] AckStartSeqs { get; set; }

        [ProtoMember(4, IsPacked = true)]
        public uint[] AckSeqCounts { get; set; }

        [ProtoMember(5)]
        public ulong BytesRead { get; set; }

        [ProtoMember(6)]
        public IList<string> ClientIds { get; set; }

        [ProtoMember(7)]
        public uint WindowSize { get; set; }

        [ProtoMember(8)]
        public uint Mtu { get; set; }

        [ProtoMember(9)]
        public bool IsClose { get; set; }

        [ProtoMember(10)]
        public bool IsHandshake { get; set; }
    }
}
