using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Ncp.Exceptions;
using Ncp.Protobuf;

namespace Ncp
{
    public class Connection
    {
        private readonly Session session;
        private readonly Channel<uint?> sendWindowUpdateChannel;
        private readonly IDictionary<uint, DateTime> sequencesSentTimes;
        private readonly IDictionary<uint, DateTime> sequencesResentTimes;
        private readonly ConcurrentPriorityQueue<uint, uint> sendAckQueue;
        
        public Connection(Session session, string localClientId, string remoteClientId)
        {
            this.session = session;

            this.LocalClientId = localClientId;
            this.RemoteClientId = remoteClientId;
            this.WindowSize = session.Options.InitialConnectionWindowSize;
            this.RetransmissionTimeout = session.Options.InitialRetransmissionTimeout;

            this.sendWindowUpdateChannel = Channel.CreateBounded<uint?>(1);            
            this.sequencesSentTimes = new ConcurrentDictionary<uint, DateTime>();
            this.sequencesResentTimes = new ConcurrentDictionary<uint, DateTime>();
            this.sendAckQueue = new ConcurrentPriorityQueue<uint, uint>();
        }

        public string LocalClientId { get; }

        public string RemoteClientId { get; }

        public int RetransmissionTimeout { get; private set; }

        public int WindowSize { get; private set; }

        public string Key => this.LocalClientId + " - " + this.RemoteClientId;

        public static string GetKey(string localClientId, string remoteClientId)
            => localClientId + " - " + remoteClientId;

        public void SendAck(uint sequenceId) => this.sendAckQueue.Enqueue(sequenceId, sequenceId);

        public void ReceiveAck(uint sequenceId, bool isSentByMe)
        {
            if (!this.sequencesSentTimes.ContainsKey(sequenceId))
            {
                return;
            }

            if (!this.sequencesResentTimes.ContainsKey(sequenceId))
            {
                this.WindowSize++;
                if (this.WindowSize > this.session.Options.MaxConnectionWindowSize)
                {
                    this.WindowSize = this.session.Options.MaxConnectionWindowSize;
                }
            }

            if (isSentByMe)
            {
                var rtt = (DateTime.Now - this.sequencesSentTimes[sequenceId]).TotalMilliseconds;

                this.RetransmissionTimeout += (int)Math.Tanh((3 * rtt - this.RetransmissionTimeout) / 1000) * 100;
                if (this.RetransmissionTimeout > this.session.Options.MaxRetransmissionTimeout)
                {
                    this.RetransmissionTimeout = this.session.Options.MaxRetransmissionTimeout;
                }
            }

            this.sequencesSentTimes.Remove(sequenceId);
            this.sequencesResentTimes.Remove(sequenceId);

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.sendWindowUpdateChannel.Push(null, cts.Token),
                Constants.ClosedChannel.Shift(cts.Token)
            };

            Task.Run(() => channelTasks.FirstAsync(cts));
        }

        private int SendWindowUsed => this.sequencesSentTimes.Count;

        private async Task WaitForSendWindowAsync(Context context)
        {
            while (this.SendWindowUsed >= this.WindowSize)
            {
                var timeoutChannel = Constants.MaximumWaitTime.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                { 
                    this.sendWindowUpdateChannel.Shift(cts.Token),
                    timeoutChannel.Shift(cts.Token),
                    context.DoneChannel.Shift(cts.Token) 
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == timeoutChannel)
                {
                    throw Constants.MaxWaitError;
                }
                else if (channel == context.DoneChannel)
                {
                    throw context.Error;
                }
            }
        }

        public void Start()
        {
            Task.Run(this.SendDataAsync);
            Task.Run(this.SendAckAsync);
            Task.Run(this.CheckTimeoutAsync);
        }
        
        private async Task SendDataAsync()
        {
            uint sequenceId = 0;
            while (true)
            {
                if (sequenceId == 0)
                {
                    sequenceId = await this.session.GetResendSequenceAsync();
                }

                if (sequenceId == 0)
                {
                    try
                    {
                        await this.WaitForSendWindowAsync(this.session.Context);
                    }
                    catch (Exception e)
                    {
                        if (e == Constants.MaxWaitError)
                        {
                            continue;
                        }

                        throw e;
                    }

                    var nextSendSequenceId = await this.session.GetSendSequenceAsync();

                    sequenceId = nextSendSequenceId.Value;
                }

                var data = this.session.GetDataToSend(sequenceId);
                if (data == null)
                {
                    this.sequencesSentTimes.Remove(sequenceId);
                    this.sequencesResentTimes.Remove(sequenceId);

                    sequenceId = 0;

                    continue;
                }

                try
                {
                    var packet = ProtoSerializer.Deserialize<Packet>(data);
                    if (packet.SequenceId != sequenceId)
                    {
                        throw new Exception("sequence missmatch");
                    }

                    Console.WriteLine($"Sending session message. Packet Id: {sequenceId}, Data Length: {packet.Data.Length}, Data: {string.Join(", ", packet.Data.Take(10))}");
                    await this.session.SendDataAsync(this.LocalClientId, this.RemoteClientId, data);
                }
                catch (Exception e)
                {
                    if (this.session.IsClosed)
                    {
                        throw new SessionClosedException();
                    }

                    var cts = new CancellationTokenSource();
                    var channelTasks = new List<Task<Channel<uint?>>>
                    {
                        this.session.ResendChannel.Push(sequenceId, cts.Token),
                        this.session.Context.DoneChannel.Shift(cts.Token)
                    };

                    var channel = await channelTasks.FirstAsync(cts);
                    if (channel == this.session.ResendChannel)
                    {
                        sequenceId = 0;
                        break;
                    }
                    else if (channel == this.session.Context.DoneChannel)
                    {
                        throw this.session.Context.Error;
                    }

                    await Task.Delay(1000);
                    continue;
                }

                if (this.sequencesSentTimes.ContainsKey(sequenceId) == false)
                {
                    this.sequencesSentTimes.Add(sequenceId, DateTime.Now);
                }

                this.sequencesResentTimes.Remove(sequenceId);

                sequenceId = 0;
            }
        }

        private async Task SendAckAsync()
        {
            while (true)
            {
                var timeoutChannel = this.session.Options.SendAckInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>> 
                { 
                    timeoutChannel.Shift(cts.Token), 
                    this.session.Context.DoneChannel.Shift(cts.Token) 
                };
                
                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.session.Context.DoneChannel)
                {
                    throw this.session.Context.Error;
                }

                if (this.sendAckQueue.Count == 0)
                {
                    continue;
                }

                var ackStartSequences = new List<uint>();
                var ackSequenceCounts = new List<uint>();

                while (this.sendAckQueue.Count > 0 && ackStartSequences.Count < this.session.Options.MaxAckSeqListSize)
                {
                    this.sendAckQueue.TryDequeue(out var result);

                    uint currentAckStartSequence = result.Key;
                    uint currentAckSequencesCount = 1;

                    this.sendAckQueue.TryPeek(out var item);
                    while (
                        this.sendAckQueue.Count > 0 
                        && item.Key == Session.NextSequenceId(currentAckStartSequence, (int)currentAckSequencesCount))
                    {
                        this.sendAckQueue.TryDequeue(out _);
                        currentAckSequencesCount++;
                    }

                    ackStartSequences.Add(currentAckStartSequence);
                    ackSequenceCounts.Add(currentAckSequencesCount);
                }

                var omitCount = true;
                foreach (var count in ackSequenceCounts)
                {
                    if (count != 1)
                    {
                        omitCount = false;
                        break;
                    }
                }

                if (omitCount)
                {
                    ackSequenceCounts = null;
                }

                try
                {
                    var packet = new Packet 
                    {
                        AckStartSeqs = ackStartSequences.ToArray(), 
                        BytesRead = this.session.TotalBytesRead 
                    };

                    if (ackSequenceCounts != null)
                    {
                        packet.AckSeqCounts = ackSequenceCounts.ToArray();
                    }

                    var data = ProtoSerializer.Serialize(packet);

                    await this.session.SendDataAsync(this.LocalClientId, this.RemoteClientId, data);

                    this.session.AckSentTime = DateTime.Now;
                }
                catch (Exception e)
                {
                    await Task.Delay(1000);
                    continue;
                }
            }
        }

        private async Task CheckTimeoutAsync()
        {
            while (true)
            {
                var timeoutChannel = this.session.Options.CheckTimeoutInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    timeoutChannel.Shift(cts.Token),
                    this.session.Context.DoneChannel.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.session.Context.DoneChannel)
                {
                    throw this.session.Context.Error;
                }

                var threshhold = DateTime.Now.AddMilliseconds(-this.RetransmissionTimeout);

                foreach (var item in this.sequencesSentTimes)
                {
                    if (this.sequencesResentTimes.ContainsKey(item.Key))
                    {
                        continue;
                    }

                    if (item.Value < threshhold)
                    {
                        await this.session.ResendChannel.Push(item.Key);

                        this.sequencesResentTimes.Add(item.Key, default);

                        this.WindowSize /= 2;
                        if (this.WindowSize < this.session.Options.MinConnectionWindowSize)
                        {
                            this.WindowSize = this.session.Options.MinConnectionWindowSize;
                        }

                        var cts2 = new CancellationTokenSource();
                        var channelTasks2 = new List<Task<Channel<uint?>>>
                        {
                            this.session.ResendChannel.Push(item.Key, cts.Token),
                            this.session.Context.DoneChannel.Shift(cts.Token)
                        };

                        var channel2 = await channelTasks2.FirstAsync(cts);
                        if (channel2 == this.session.ResendChannel)
                        {
                            this.sequencesResentTimes.Add(item.Key, default);
                            this.WindowSize /= 2;
                            if (this.WindowSize < this.session.Options.MinConnectionWindowSize)
                            {
                                this.WindowSize = this.session.Options.MinConnectionWindowSize;
                            }
                        }
                        else if (channel2 == this.session.Context.DoneChannel)
                        {
                            throw this.session.Context.Error;
                        }
                    }
                }
            }
        }
    }
}
