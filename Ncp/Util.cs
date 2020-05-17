using Open.ChannelExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ncp
{
    public static class Util
    {
        public static int NextSequence(int sequenceId, int step)
        {
            var max = uint.MaxValue - Constants.MinSequenceId + 1;
            var result = (sequenceId - Constants.MinSequenceId + step) % max;
            if (result < 0)
            {
                result += max;
            }

            return (int)result + Constants.MinSequenceId;
        }

        public static bool IsSequenceInBetween(int start, int end, int target)
        {
            if (start <= end)
            {
                return target >= start && target < end;
            }

            return target >= start || target < end;
        }

        public static Channel<int?> MakeTimeoutChannel(int timeout)
        {
            var channel = Channel.CreateUnbounded<int?>();
            Task.Run(async delegate
            {
                await Task.Delay(timeout);
                await channel.CompleteAsync();
            });

            return channel;
        }

        public static async Task<Task> MakeTimeoutTaskAsync(Task task, int timeout)
        {
            var currentTaskCancellationTokenSource = new CancellationTokenSource();
            return await Task.Factory.StartNew(async delegate
            {
                var cancellationTokenSource = new CancellationTokenSource();
                Task timer;
                if (timeout > 0)
                {
                    timer = Task.Run(async delegate {
                        await Task.Delay(timeout);
                        currentTaskCancellationTokenSource.Cancel();
                    }, cancellationTokenSource.Token);
                }

                await task.ContinueWith(x =>
                {
                    cancellationTokenSource.Cancel();
                });

            }, currentTaskCancellationTokenSource.Token);
        }

        public static int CompareSeq(int seq1, int seq2)
        {
            if (seq1 == seq2)
            {
                return 0;
            }

            if (seq1 < seq2)
            {
                if (seq2 - seq1 < int.MaxValue / 2)
                {
                    return -1;
                }

                return 1;
            }

            if (seq1 - seq2 < int.MaxValue / 2)
            {
                return 1;
            }

            return -1;
        }
    }
}
