using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Open.ChannelExtensions;

namespace Ncp
{
    public static class Extensions
    {
        public static Task<T> ShiftValue<T>(this Channel<T> channel, CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                try
                {
                    return await channel.Reader.ReadAsync().AsTask();
                }
                catch (ChannelClosedException)
                {
                    return default;
                }
            }, token);
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

        public static async Task<Channel<T>> FirstAsync<T>(this IEnumerable<Task<Channel<T>>> tasks, CancellationTokenSource tokenSource = default)
        {
            var task = await Task.WhenAny(tasks);

            var channel = await task;

            tokenSource.Cancel();

            return channel;
        }

        public static async Task<T> FirstValueAsync<T>(this IEnumerable<Task<T>> tasks, CancellationTokenSource tokenSource = default)
        {
            try
            {
                var task = await Task.WhenAny(tasks);

                var result = await task;

                tokenSource.Cancel();

                return result;
            }
            catch (ChannelClosedException)
            {
                return default;
            }
        }

        public static Channel<uint?> ToTimeoutChannel(this int timeout)
        {
            var channel = Channel.CreateBounded<uint?>(1);
            Task.Run(async delegate
            {
                await Task.Delay(timeout);
                await channel.CompleteAsync();
            });

            return channel;
        }

        public static Task<Task> ToTimeoutTask(this Task task, int timeout, Exception error)
        {
            var mainTaskCancellationTokenSource = new CancellationTokenSource();
            return Task.Factory.StartNew(async delegate
            {
                var cancellationTokenSource = new CancellationTokenSource();
                Task timer;
                if (timeout > 0)
                {
                    timer = Task.Run(async delegate {
                        await Task.Delay(timeout);

                        mainTaskCancellationTokenSource.Cancel();

                        throw error;
                    }, cancellationTokenSource.Token);
                }

                await task.ContinueWith(x =>
                {
                    cancellationTokenSource.Cancel();
                });

            }, mainTaskCancellationTokenSource.Token);
        }
    }
}
