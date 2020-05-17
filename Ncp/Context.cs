using Open.ChannelExtensions;
using System;
using System.Linq;
using System.Linq.Expressions;

using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace Ncp
{
    public class Context
    {
        private Channel<int?> cancelChannel;
        private Task timeoutTask;
        private Exception error;

        private Context(Context parent = null, bool cancel = false, int timeout = 0)
        {
            this.Done = Channel.CreateBounded<int?>(1);

            Channel<int?> parentChan = null;
            Channel<int?> timeoutChan = null;
            var channels = new List<Channel<int?>>();

            if (parent != null)
            {
                parentChan = parent.Done;
                channels.Add(parentChan);
            }

            if (cancel)
            {
                this.cancelChannel = Channel.CreateBounded<int?>(1);
                channels.Add(this.cancelChannel);
            }

            if (timeout > 0)
            {
                timeoutChan = Channel.CreateBounded<int?>(1);
                channels.Add(timeoutChan);

                this.timeoutTask = Task.Run(async delegate
                {
                    await Task.Delay(timeout);
                    await timeoutChan.CompleteAsync();
                });
            }

            if (channels.Count > 0)
            {
                NcpChannel.SelectChannel(channels).ContinueWith(async x =>
                {
                    var channel = await x;

                    Exception ex = null;
                    if (channel == parentChan)
                    {
                        ex = parent.Error;
                    }
                    else if (channel == this.cancelChannel)
                    {
                        throw new Exception();
                    }
                    else if (channel == timeoutChan)
                    {
                        throw new Exception();
                    }

                    this.error = ex;

                    await this.Done.CompleteAsync();
                    await this.CancelAsync();
                    this.timeoutTask.Dispose();
                });
            }
        }

        public Channel<int?> Done { get; }

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
