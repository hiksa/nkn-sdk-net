using Ncp.Protobuf;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NknSdk.Tests.Ncp
{
    public class ProtoSerializerTests
    {
        [Fact]
        public void Packet_ShouldSerialize_Properly()
        {
            var input = new List<(uint[] ackStartSeqList, uint[] ackSeqCountList, uint bytesRead, byte[] result)>
            {
                (new uint[] { 1 }, new uint[] { }, 1, new byte[] { 26, 1, 1, 40, 1 }),
                (new uint[] { 6 }, new uint[] { }, 6, new byte[] { 26, 1, 6, 40, 6 }),
                (new uint[] { 7 }, new uint[] { }, 7, new byte[] { 26, 1, 7, 40, 7 }),
                (new uint[] { 8 }, new uint[] { }, 8, new byte[] { 26, 1, 8, 40, 8 }),
            };

            foreach (var (ackStartSeqList, ackSeqCountList, BytesRead, Result) in input)
            {
                var packet = new Packet
                {
                    AckStartSeqs = ackStartSeqList,
                    BytesRead = BytesRead
                };

                var serialized = ProtoSerializer.Serialize(packet);

                Assert.Equal(Result, serialized);
            }
        }
    }
}
