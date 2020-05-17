using Ncp;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NknSdk.Tests.Ncp
{
    public class UtilTests
    {
        [Fact]
        public void NextSequence_Should_ReturnCorrectValue()
        {
            var input = new List<(int SequenceId, int Step, int Expected)>
            {
                (1, 1, 2),
                (2, 1, 3),
                (1, 2, 3),
                (3, -1, 2)
            };

            foreach (var item in input)
            {
                var result = Util.NextSequence(item.SequenceId, item.Step);

                Assert.Equal(item.Expected, result);
            }
        }

        [Fact]
        public void IsSequenceInBetween_Should_ReturnCorrectValue()
        {
            var input = new List<(int Start, int End, int Target, bool Expected)>
            {
                (1, 3, 1, true),
                (1, 2, 1, true),
                (1, 2, 2, false),
                (2, 3, 2, true),
                (2, 3, 3, false),
            };

            foreach (var item in input)
            {
                var result = Util.IsSequenceInBetween(item.Start, item.End, item.Target);

                Assert.Equal(item.Expected, result);
            }
        }
    }
}
