using System;
using System.Collections.Generic;
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
                    return await channel.Reader.ReadAsync(token).AsTask();
                }
                catch (ChannelClosedException)
                {
                    return default;
                }
                catch (OperationCanceledException)
                {
                    return default;
                }
            }, token);
        }

        public static Task<Channel<T>> WaitToRead<T>(this Channel<T> channel, CancellationToken token = default)
        {
            return Task.Run(async delegate
            {
                try
                {
                    await channel.Reader.WaitToReadAsync(token);
                }
                catch (ChannelClosedException)
                {
                    return channel;
                }
                catch (OperationCanceledException)
                {
                    return channel;
                }

                return channel;
            }, token);
        }

        public static Task<Channel<T>> Shift<T>(this Channel<T> channel, CancellationToken token = default)
        {
            return Task.Run(async delegate
            {
                try
                {
                    await channel.Reader.ReadAsync(token);
                }
                catch (ChannelClosedException)
                {
                    return channel;
                }
                catch (OperationCanceledException)
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
                try
                {
                    await channel.Writer.WriteAsync(value, token);
                }
                catch (ChannelClosedException)
                {
                    return channel;
                }
                catch (OperationCanceledException)
                {
                    return channel;
                }

                return channel;
            }, token);
        }

        public static async Task<Channel<T>> FirstAsync<T>(this IEnumerable<Task<Channel<T>>> tasks, CancellationTokenSource tokenSource = default)
        {
            var channel = await await Task.WhenAny(tasks);

            tokenSource.Cancel();

            return channel;
        }

        public static async Task<T> FirstValueAsync<T>(this IEnumerable<Task<T>> tasks, CancellationTokenSource tokenSource = default)
        {
            try
            {
               // await Task.Delay(2);

                var result = await await Task.WhenAny(tasks);

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
            Timer timer = default;
                
            timer = new Timer(async state =>
            {
                await channel.CompleteAsync();
                timer.Dispose();
            },
            null,
            timeout,
            Timeout.Infinite);

            return channel;
        }

        public static Task ToTimeoutTask(this Task task, int timeout, Exception error)
        {
            Timer timer = null;
            var hasTimedOut = false;

            if (timeout > 0)
            {
                timer = new Timer(state => hasTimedOut = true, null, timeout, Timeout.Infinite);
            }

            return Task.Run(() =>
            {
                while (task.IsCompleted == false)
                {
                    if (hasTimedOut)
                    {
                        throw error;
                    }
                }

                timer.Dispose();
            });
        }
    }
}
