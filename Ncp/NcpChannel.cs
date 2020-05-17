using Open.ChannelExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ncp
{
    public static class NcpChannel
    {
        private static Channel<int?> closedChannel;

        public static Channel<int?> ClosedChannel
        {
            get
            {
                if (closedChannel == null)
                {
                    closedChannel = Channel.CreateUnbounded<int?>();
                    closedChannel.Writer.Complete();
                }

                return closedChannel;
            }
        }

        public static async Task<Channel<T>> SelectChannel<T>(params Channel<T>[] channels)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            
            var tasksWaitingToRead = channels.Select(async ch => {
                await ch.Reader.WaitToReadAsync(cancellationTokenSource.Token);
                return ch;
            });

            var taskReadyToRead = await Task.WhenAny(tasksWaitingToRead);
            var channel = await taskReadyToRead;

            cancellationTokenSource.Cancel();

            return channel;
        }

        public static async Task<Channel<T>> SelectChannel<T>(IEnumerable<Channel<T>> channels)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var tasksWaitingToRead = channels.Select(async ch => {
                await ch.Reader.WaitToReadAsync(cancellationTokenSource.Token);
                return ch;
            });

            var taskReadyToRead = await Task.WhenAny(tasksWaitingToRead);
            var channel = await taskReadyToRead;

            cancellationTokenSource.Cancel();

            return channel;
        }

        public static async Task<T> SelectValue<T>(params Channel<T>[] channels)
        {
            return await (await SelectChannel(channels)).Reader.ReadAsync();
        }

        public static async Task<Channel<T>> SelectFromTask<T>(CancellationTokenSource tokenSource = default, params Task<Channel<T>>[] tasks)
        {
            var taskReadyToRead = await Task.WhenAny(tasks);

            var channel = await taskReadyToRead;

            tokenSource.Cancel();

            return channel;
        }

        public static Task<Channel<T>> Shift<T>(this Channel<T> channel, CancellationToken token = default)
        {
            return Task.Run(async delegate
            {
                try
                {
                    await channel.Reader.ReadAsync();
                }
                catch (ChannelClosedException)
                {
                    return channel;
                }

                return channel;
            }, token);
        }

        public static Task<Channel<T>> Push<T>(this Channel<T> channel, T value, CancellationToken token = default)
        {
            return Task.Run(async delegate
            {
                await channel.Writer.WriteAsync(value);
                return channel;
            }, token);
        }
    }
}
