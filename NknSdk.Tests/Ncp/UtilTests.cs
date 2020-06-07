using Ncp;
using Ncp.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NknSdk.Tests.Ncp
{
    public class UtilTests
    {
        [Fact]
        public void NextSequence_Should_ReturnCorrectValue()
        {
            var input = new List<(uint SequenceId, int Step, uint Expected)>
            {
                (1, 1, 2),
                (2, 1, 3),
                (1, 2, 3),
                (3, -1, 2)
            };

            foreach (var item in input)
            {
                var result = Session.NextSequenceId(item.SequenceId, item.Step);

                Assert.Equal(item.Expected, result);
            }
        }

        [Fact]
        public void IsSequenceInBetween_Should_ReturnCorrectValue()
        {
            var input = new List<(uint Start, uint End, uint Target, bool Expected)>
            {
                (1, 3, 1, true),
                (1, 2, 1, true),
                (1, 2, 2, false),
                (2, 3, 2, true),
                (2, 3, 3, false),
            };

            foreach (var item in input)
            {
                var result = Session.IsSequenceInbetween(item.Start, item.End, item.Target);

                Assert.Equal(item.Expected, result);
            }
        }

        [Fact]
        public void MakeTimeoutTask_TaskShouldTimeout()
        {
            var task = Task.Factory.StartNew(async delegate
            {
                await Task.Delay(5_000);
                Console.WriteLine("Task completed");
            });

            var timeout = task.ToTimeoutTask(10_000, new WriteDeadlineExceededException());

            var a = Task.WhenAny(task, timeout).GetAwaiter().GetResult();

        }
    }
}
