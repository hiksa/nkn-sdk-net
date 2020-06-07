using Ncp.Exceptions;
using Ncp.Protobuf;
using Open.ChannelExtensions;
using Priority_Queue;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ncp
{
    public class Connection
    {
        private readonly Session session;

        private readonly Channel<uint?> sendWindowUpdate;

        private readonly IDictionary<uint, DateTime> timeSentSeq;
        private readonly IDictionary<uint, DateTime> resentSeq;
        private readonly ConcurrentPriorityQueue<uint, uint> sendAckQueue;
        
        public Connection(Session session, string localClientId, string remoteClientId)
        {
            this.session = session;
            this.LocalClientId = localClientId;
            this.RemoteClientId = remoteClientId;

            this.WindowSize = session.Config.InitialConnectionWindowSize;
            this.RetransmissionTimeout = session.Config.InitialRetransmissionTimeout;

            this.sendWindowUpdate = Channel.CreateBounded<uint?>(1);

            this.timeSentSeq = new ConcurrentDictionary<uint, DateTime>();
            this.resentSeq = new ConcurrentDictionary<uint, DateTime>();
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

        public void ReceiveAckAsync(uint sequenceId, bool isSentByMe)
        {
            if (!this.timeSentSeq.ContainsKey(sequenceId))
            {
                return;
            }

            if (!this.resentSeq.ContainsKey(sequenceId))
            {
                this.WindowSize++;
                if (this.WindowSize > this.session.Config.MaxConnectionWindowSize)
                {
                    this.WindowSize = this.session.Config.MaxConnectionWindowSize;
                }
            }

            if (isSentByMe)
            {
                var rtt = (DateTime.Now - this.timeSentSeq[sequenceId]).TotalMilliseconds;

                this.RetransmissionTimeout += (int)Math.Tanh((3 * rtt - this.RetransmissionTimeout) / 1000) * 100;
                if (this.RetransmissionTimeout > this.session.Config.MaxRetransmissionTimeout)
                {
                    this.RetransmissionTimeout = this.session.Config.MaxRetransmissionTimeout;
                }
            }

            this.timeSentSeq.Remove(sequenceId);
            this.resentSeq.Remove(sequenceId);

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.sendWindowUpdate.Push(null, cts.Token),
                Constants.ClosedChannel.Shift(cts.Token)
            };

            Task.Run(() => channelTasks.FirstAsync(cts));
        }

        private int SendWindowUsed() => this.timeSentSeq.Count;

        private int SendAckQueueLength() => this.sendAckQueue.Count;

        private async Task WaitForSendWindowAsync(Context context)
        {
            while (this.SendWindowUsed() >= this.WindowSize)
            {
                var timeout = Constants.MaximumWaitTime.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                { 
                    this.sendWindowUpdate.Shift(cts.Token),
                    timeout.Shift(cts.Token),
                    context.Done.Shift(cts.Token) 
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == timeout)
                {
                    throw Constants.MaxWaitError;
                }
                else if (channel == context.Done)
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
            uint seq = 0;
            while (true)
            {
                if (seq == 0)
                {
                    seq = await this.session.GetResendSequenceAsync();
                }

                if (seq == 0)
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

                    var nextSendSequence = await this.session.GetSendSequenceAsync();

                    seq = nextSendSequence.Value;
                }

                var buffer = this.session.GetDataToSend(seq);
                if (buffer == null)
                {
                    this.timeSentSeq.Remove(seq);
                    this.resentSeq.Remove(seq);
                    seq = 0;
                    continue;
                }

                try
                {
                    //this.session.Test.AddRange(buffer);
                    var packet = ProtoSerializer.Deserialize<Packet>(buffer);
                    this.session.Buffers.TryAdd(seq, packet.Data);
                    if (packet.SequenceId != seq)
                    {
                        throw new Exception("sequence missmatch");
                    }

                    Console.WriteLine($"Sending session message. Packet Id: {seq}, Data Length: {packet.Data.Length}, Data: {string.Join(", ", packet.Data.Take(10))}");
                    await this.session.SendWithAsync(this.LocalClientId, this.RemoteClientId, buffer);
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
                        this.session.resendChannel.Push(seq, cts.Token),
                        this.session.Context.Done.Shift(cts.Token)
                    };

                    var channel = await channelTasks.FirstAsync(cts);
                    if (channel == this.session.resendChannel)
                    {
                        seq = 0;
                        break;
                    }
                    else if (channel == this.session.Context.Done)
                    {
                        throw this.session.Context.Error;
                    }

                    await Task.Delay(1000);
                    continue;
                }

                if (this.timeSentSeq.ContainsKey(seq) == false)
                {
                    this.timeSentSeq.Add(seq, DateTime.Now);
                }

                this.resentSeq.Remove(seq);

                seq = 0;
            }
        }

        private async Task SendAckAsync()
        {
            while (true)
            {
                var timeout = this.session.Config.SendAckInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>> 
                { 
                    timeout.Shift(cts.Token), 
                    this.session.Context.Done.Shift(cts.Token) 
                };
                
                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.session.Context.Done)
                {
                    throw this.session.Context.Error;
                }

                if (this.SendAckQueueLength() == 0)
                {
                    continue;
                }

                var ackStartSeqList = new List<uint>();
                var ackSeqCountList = new List<uint>();

                while (this.SendAckQueueLength() > 0 && ackStartSeqList.Count < this.session.Config.MaxAckSeqListSize)
                {
                    this.sendAckQueue.TryDequeue(out var result);
                    uint ackStartSeq = result.Key;
                    uint ackSeqCount = 1;

                    this.sendAckQueue.TryPeek(out var item);
                    while (this.SendAckQueueLength() > 0 && item.Key == Session.NextSequenceId(ackStartSeq, (int)ackSeqCount))
                    {
                        this.sendAckQueue.TryDequeue(out _);
                        ackSeqCount++;
                    }

                    ackStartSeqList.Add(ackStartSeq);
                    ackSeqCountList.Add(ackSeqCount);
                }

                var omitCount = true;
                foreach (var c in ackSeqCountList)
                {
                    if (c != 1)
                    {
                        omitCount = false;
                        break;
                    }
                }

                if (omitCount)
                {
                    ackSeqCountList = null;
                }

                try
                {
                    var packet = new Packet 
                    {
                        AckStartSeqs = ackStartSeqList.ToArray(), 
                        BytesRead = this.session.BytesRead 
                    };

                    if (ackSeqCountList != null)
                    {
                        packet.AckSeqCounts = ackSeqCountList.ToArray();
                    }

                    var buffer = ProtoSerializer.Serialize(packet);

                    await this.session.SendWithAsync(this.LocalClientId, this.RemoteClientId, buffer);

                    this.session.BytesReadSentTime = DateTime.Now;
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
                var timeout = this.session.Config.CheckTimeoutInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    timeout.Shift(cts.Token),
                    this.session.Context.Done.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.session.Context.Done)
                {
                    throw this.session.Context.Error;
                }

                var threshhold = DateTime.Now.AddMilliseconds(-this.RetransmissionTimeout);

                foreach (var item in this.timeSentSeq)
                {
                    if (this.resentSeq.ContainsKey(item.Key))
                    {
                        continue;
                    }

                    if (item.Value < threshhold)
                    {
                        await this.session.resendChannel.Push(item.Key);

                        this.resentSeq.Add(item.Key, default);

                        this.WindowSize /= 2;
                        if (this.WindowSize < this.session.Config.MinConnectionWindowSize)
                        {
                            this.WindowSize = this.session.Config.MinConnectionWindowSize;
                        }

                        var cts2 = new CancellationTokenSource();
                        var channelTasks2 = new List<Task<Channel<uint?>>>
                        {
                            this.session.resendChannel.Push(item.Key, cts.Token),
                            this.session.Context.Done.Shift(cts.Token)
                        };

                        var channel2 = await channelTasks2.FirstAsync(cts);
                        if (channel2 == this.session.resendChannel)
                        {
                            this.resentSeq.Add(item.Key, default);
                            this.WindowSize /= 2;
                            if (this.WindowSize < this.session.Config.MinConnectionWindowSize)
                            {
                                this.WindowSize = this.session.Config.MinConnectionWindowSize;
                            }
                        }
                        else if (channel2 == this.session.Context.Done)
                        {
                            throw this.session.Context.Error;
                        }
                    }
                }
            }
        }
    }
}
