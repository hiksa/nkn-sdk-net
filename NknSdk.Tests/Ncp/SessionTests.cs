using Ncp;
using Ncp.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NknSdk.Tests.Ncp
{
    public class SessionTests
    {
        [Fact]
        public void NextSequence_Should_ReturnCorrectValue()
        {
            var input = new List<(uint sequenceId, int step, uint expected)>
            {
                (1, 1, 2),
                (2, 1, 3),
                (1, 2, 3),
                (3, -1, 2)
            };

            foreach (var (sequenceId, step, expected) in input)
            {
                var result = Session.NextSequenceId(sequenceId, step);
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public void IsSequenceInBetween_Should_ReturnCorrectValue()
        {
            var input = new List<(uint start, uint end, uint target, bool expected)>
            {
                (1, 3, 1, true),
                (1, 2, 1, true),
                (1, 2, 2, false),
                (2, 3, 2, true),
                (2, 3, 3, false),
            };

            foreach (var (start, end, target, expected) in input)
            {
                var result = Session.IsSequenceInbetween(start, end, target);
                Assert.Equal(expected, result);
            }
        }
    }
}
