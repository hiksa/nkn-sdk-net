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
        public static Task<Task> MakeTimeoutTask(this Task task, int timeout, Exception error)
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
