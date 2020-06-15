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
        private readonly Timer timeoutTimer;

        private Context(Context parent = null, bool cancel = false, int timeout = 0)
        {
            this.DoneChannel = Channel.CreateBounded<uint?>(1);

            Channel<uint?> parentChannel = null;
            Channel<uint?> timeoutChannel = null;
            var tasks = new List<Task<Channel<uint?>>>();
            var cts = new CancellationTokenSource();

            if (parent != null)
            {
                parentChannel = parent.DoneChannel;
                tasks.Add(parentChannel.Shift(cts.Token));
            }

            if (cancel)
            {
                this.cancelChannel = Channel.CreateBounded<uint?>(1);
                tasks.Add(this.cancelChannel.Shift(cts.Token));
            }

            if (timeout > 0)
            {
                timeoutChannel = timeout.ToTimeoutChannel();
                tasks.Add(timeoutChannel.Shift(cts.Token));

                this.timeoutTimer = new Timer(
                    (state) => timeoutChannel.Writer.Complete(), 
                    null, 
                    timeout, 
                    Timeout.Infinite);
            }

            if (tasks.Count > 0)
            {
                tasks
                    .FirstAsync(cts)
                    .ContinueWith(async task =>
                    {
                        var channel = await task;
                        if (channel == parentChannel)
                        {
                            this.Error = parent.Error;
                        }
                        else if (channel == this.cancelChannel)
                        {
                            this.Error = new ContextCanceledException();
                        }
                        else if (channel == timeoutChannel)
                        {
                            this.Error = new ContextExpiredException();
                        }

                        await this.DoneChannel.CompleteAsync();
                        await this.CancelAsync();

                        this.timeoutTimer?.Dispose();
                    });
            }
        }

        public Channel<uint?> DoneChannel { get; }

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
