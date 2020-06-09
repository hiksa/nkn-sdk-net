using Ncp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace NknSdk.Tests.Ncp
{
    public class ChannelExtensionsTests
    {
        [Fact]
        public void ActiveChannel_Should_BeSelectedFirst()
        {
            for (int i = 0; i < 10; i++)
            {
                var onAccept = Channel.CreateBounded<uint?>(1);
                onAccept.Writer.WriteAsync(1);

                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    onAccept.Shift(cts.Token),
                    Constants.ClosedChannel.Shift(cts.Token)
                };

                var channel = channelTasks
                    .FirstAsync(cts)
                    .GetAwaiter()
                    .GetResult();

                Assert.Equal(onAccept, channel);
            }
        }

        [Fact]
        public void Should_Select_CorrectValue()
        {
            uint value = 5;
            var firstChannel = Channel.CreateBounded<uint?>(1);
            firstChannel
                .Writer
                .WriteAsync(value)
                .GetAwaiter()
                .GetResult();

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<uint?>>
            {
                firstChannel.ShiftValue(cts.Token),
                Constants.ClosedChannel.ShiftValue(cts.Token)
            };

            var result = channelTasks
                .FirstValueAsync(cts)
                .GetAwaiter()
                .GetResult();

            Assert.Equal(value, result.GetValueOrDefault());
        }
    }
}
