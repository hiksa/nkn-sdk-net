using Open.ChannelExtensions;
using System;
using System.Linq;
using System.Linq.Expressions;

using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;
using Ncp.Exceptions;

namespace Ncp
{
    public class Context
    {
        private Channel<uint?> cancelChannel;
        private Task timeoutTask;
        private Exception error;

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
                            this.error = parent.Error;
                        }
                        else if (channel == this.cancelChannel)
                        {
                            this.error = new ContextCanceledException();
                        }
                        else if (channel == timeoutChan)
                        {
                            this.error = new ContextExpiredException();
                        }

                        await this.Done.CompleteAsync();
                        await this.CancelAsync();

                        this.timeoutTask.Dispose();
                    });
            }
        }

        public Channel<uint?> Done { get; }

        public Exception Error => this.error;

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
