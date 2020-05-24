using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;

using Open.ChannelExtensions;

using Ncp.Exceptions;

namespace Ncp
{
    public class Context
    {
        private readonly Channel<uint?> cancelChannel;
        private readonly Task timeoutTask;

        private Context(Context parent = null, bool cancel = false, int timeout = 0)
        {
            this.Done = Channel.CreateBounded<uint?>(1);

            Channel<uint?> parentChan = null;
            Channel<uint?> timeoutChan = null;
            var tasks = new List<Task<Channel<uint?>>>();
            var cts = new CancellationTokenSource();

            if (parent != null)
            {
                parentChan = parent.Done;
                tasks.Add(parentChan.Shift(cts.Token));
            }

            if (cancel)
            {
                this.cancelChannel = Channel.CreateBounded<uint?>(1);
                tasks.Add(this.cancelChannel.Shift(cts.Token));
            }

            if (timeout > 0)
            {
                timeoutChan = timeout.ToTimeoutChannel();
                tasks.Add(timeoutChan.Shift(cts.Token));
            }

            if (tasks.Count > 0)
            {
                tasks
                    .SelectAsync(cts)
                    .ContinueWith(async task =>
                    {
                        var channel = await task;
                        if (channel == parentChan)
                        {
                            this.Error = parent.Error;
                        }
                        else if (channel == this.cancelChannel)
                        {
                            this.Error = new ContextCanceledException();
                        }
                        else if (channel == timeoutChan)
                        {
                            this.Error = new ContextExpiredException();
                        }

                        await this.Done.CompleteAsync();
                        await this.CancelAsync();

                        this.timeoutTask.Dispose();
                    });
            }
        }

        public Channel<uint?> Done { get; }

        public Exception Error { get; private set; }

        public async Task CancelAsync()
        {
            try
            {
                if (this.cancelChannel != null)
                {
                    await this.cancelChannel.CompleteAsync();
                }
            }
            catch (Exception e)
            {
            }
        }

        public static Context Background() => new Context();

        public static Context WithCancel(Context parent) => new Context(parent, true);

        public static Context WithTimeout(Context parent, int timeout) => new Context(parent, true, timeout);
    }
}
