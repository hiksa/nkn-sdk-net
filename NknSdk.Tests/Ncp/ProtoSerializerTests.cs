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
            var input = new List<(uint[] AckStartSeqList, uint[] AckSeqCountList, uint BytesRead, byte[] Result)>
            {
                (new uint[] { 1 }, new uint[] { }, 1, new byte[] { 26, 1, 1, 40, 1 }),
                (new uint[] { 6 }, new uint[] { }, 6, new byte[] { 26, 1, 6, 40, 6 }),
                (new uint[] { 7 }, new uint[] { }, 7, new byte[] { 26, 1, 7, 40, 7 }),
                (new uint[] { 8 }, new uint[] { }, 8, new byte[] { 26, 1, 8, 40, 8 }),
            };

            foreach (var item in input)
            {
                var packet = new Packet2
                {
                    AckStartSeqs = item.AckStartSeqList,
                    BytesRead = item.BytesRead
                };

                var serialized = ProtoSerializer.Serialize(packet);

                Assert.Equal(item.Result, serialized);
            }
        }
    }
}
