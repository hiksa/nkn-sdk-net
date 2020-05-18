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
    public static class ChannelExtensions
    {
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
                    await Task.Yield();
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

        public static async Task<Channel<T>> SelectChannel<T>(this IEnumerable<Channel<T>> channels)
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

        public static async Task<T> SelectValueAsync<T>(params Channel<T>[] channels)
        {
            var channel = await SelectChannel(channels);

            try
            {
                return await channel.Reader.ReadAsync();
            }
            catch (ChannelClosedException)
            {
                return default;
            }
        }

        public static async Task<Channel<T>> SelectAsync<T>(CancellationTokenSource tokenSource = default, params Task<Channel<T>>[] tasks)
        {
            var task = await Task.WhenAny(tasks);

            var channel = await task;

            tokenSource.Cancel();

            return channel;
        }

        public static async Task<Channel<T>> SelectAsync<T>(this IEnumerable<Task<Channel<T>>> tasks, CancellationTokenSource tokenSource = default)
        {
            var task = await Task.WhenAny(tasks);

            var channel = await task;

            tokenSource.Cancel();

            return channel;
        }

        public static Channel<uint?> ToTimeoutChannel(this int timeout)
        {
            var channel = Channel.CreateUnbounded<uint?>();
            Task.Run(async delegate
            {
                await Task.Delay(timeout);
                await channel.CompleteAsync();
            });

            return channel;
        }
    }
}
