using Ncp.Protobuf;
using Open.ChannelExtensions;
using Priority_Queue;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ncp
{
    public class Connection
    {
        private readonly Session session;

        private readonly Channel<int?> sendWindowUpdate;

        private readonly IDictionary<int, DateTime> timeSentSeq;
        private readonly IDictionary<int, DateTime> resentSeq;
        private readonly SimplePriorityQueue<int> sendAckQueue;

        public Connection(Session session, string localClientId, string remoteClientId)
        {
            this.session = session;
            this.LocalClientId = localClientId;
            this.RemoteClientId = remoteClientId;

            this.WindowSize = session.Config.InitialConnectionWindowSize;
            this.RetransmissionTimeout = session.Config.InitialRetransmissionTimeout;

            this.sendWindowUpdate = Channel.CreateBounded<int?>(10);

            this.timeSentSeq = new Dictionary<int, DateTime>();
            this.resentSeq = new Dictionary<int, DateTime>();
            this.sendAckQueue = new SimplePriorityQueue<int>();
        }

        public string LocalClientId { get; }

        public string RemoteClientId { get; }

        public int RetransmissionTimeout { get; private set; }

        public int WindowSize { get; private set; }

        public string Key => this.LocalClientId + " - " + this.RemoteClientId;

        public static string MakeConnectionKey(string localClientId, string remoteClientId)
            => localClientId + " - " + remoteClientId;

        public int SendWindowUsed() => this.timeSentSeq.Count;

        public void SendAck(int sequenceId) => this.sendAckQueue.Enqueue(sequenceId, sequenceId);

        public int SendAckQueueLength() => this.sendAckQueue.Count;

        public async Task ReceiveAck(int sequenceId, bool isSentByMe)
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
                var rtt = (DateTime.Now - this.timeSentSeq[sequenceId]).TotalSeconds;

                this.RetransmissionTimeout += (int)Math.Tanh((3 * rtt - this.RetransmissionTimeout) / 1000) * 100;
                if (this.RetransmissionTimeout > this.session.Config.MaxRetransmissionTimeout)
                {
                    this.RetransmissionTimeout = this.session.Config.MaxRetransmissionTimeout;
                }
            }

            this.timeSentSeq.Remove(sequenceId);
            this.resentSeq.Remove(sequenceId);

            await this.sendWindowUpdate.Writer.WriteAsync(null);
            await NcpChannel.SelectChannel(this.sendWindowUpdate, NcpChannel.ClosedChannel);
        }

        public async Task WaitForSendWindow(Context context)
        {
            while (this.SendWindowUsed() >= this.WindowSize)
            {
                var timeout = Util.MakeTimeoutChannel(Constants.MaximumWaitTime);
                var result = await NcpChannel.SelectChannel(this.sendWindowUpdate, timeout, context.Done);
                if (result == timeout)
                {
                    throw new Exception();
                }
                else if (result == context.Done)
                {
                    throw context.Error;
                }
            }
        }

        public void Start()
        {
            Task.Factory.StartNew(this.Tx);
            Task.Factory.StartNew(this.SendAck);
            Task.Factory.StartNew(this.CheckTimeout);
        }
        
        public async Task Tx()
        {
            var seq = 0;
            while (true)
            {
                if (seq == 0)
                {
                    seq = await this.session.GetResendSeq();
                }

                if (seq == 0)
                {
                    try
                    {
                        await this.WaitForSendWindow(this.session.Context);
                    }
                    catch (Exception e)
                    {
                        //TODO ...
                    }

                    seq = (await this.session.GetSendSequence()).Value;
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
                    await this.session.SendWith(this.LocalClientId, this.RemoteClientId, buffer);
                }
                catch (Exception)
                {
                    if (this.session.IsClosed)
                    {
                        throw new Exception();
                    }

                    await this.session.resendChan.Writer.WriteAsync(seq);
                    var channel = await NcpChannel.SelectChannel(this.session.resendChan, this.session.Context.Done);
                    if (channel == this.session.resendChan)
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

        public async Task SendAck()
        {
            while (true)
            {
                var timeout = Util.MakeTimeoutChannel(this.session.Config.SendAckInterval);
                var channel = await NcpChannel.SelectChannel(timeout, this.session.Context.Done);

                if (channel == this.session.Context.Done)
                {
                    throw this.session.Context.Error;
                }

                if (this.SendAckQueueLength() == 0)
                {
                    continue;
                }

                var ackStartSeqList = new List<int>();
                var ackSeqCountList = new List<int>();

                while (this.SendAckQueueLength() > 0 && ackStartSeqList.Count < this.session.Config.MaxAckSeqListSize)
                {
                    var ackStartSeq = this.sendAckQueue.Dequeue();
                    var ackSeqCount = 0;
                    while (this.SendAckQueueLength() > 0 && this.sendAckQueue.First == Util.NextSequence(ackStartSeq, ackSeqCount))
                    {
                        this.sendAckQueue.Dequeue();
                        ackSeqCount++;
                    }

                    ackStartSeqList.Add(ackStartSeq);
                    ackSeqCountList.Add(ackSeqCount);

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
                        var packet = new Packet { AckStartSeq = ackStartSeqList.ToArray(), BytesRead = this.session.BytesRead };
                        if (ackSeqCountList != null)
                        {
                            packet.AckSeqCount = ackSeqCountList.ToArray();
                        }

                        var buffer = ProtoSerializer.Serialize(packet);

                        await this.session.SendWith(this.LocalClientId, this.RemoteClientId, buffer);

                        this.session.BytesReadSentTime = DateTime.Now;
                    }
                    catch (Exception e)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }
            }
        }

        public async Task CheckTimeout()
        {
            while (true)
            {
                var timeout = Util.MakeTimeoutChannel(this.session.Config.CheckTimeoutInterval);
                var channel = await NcpChannel.SelectChannel(timeout, this.session.Context.Done);

                if (channel == this.session.Context.Done)
                {
                    throw this.session.Context.Error;
                }

                var threshhold = DateTime.Now.AddSeconds(-this.RetransmissionTimeout);

                foreach (var item in this.timeSentSeq)
                {
                    if (this.resentSeq.ContainsKey(item.Key))
                    {
                        continue;
                    }

                    if (item.Value < threshhold)
                    {
                        await this.session.resendChan.Writer.WriteAsync(item.Key);
                        this.resentSeq.Add(item.Key, default);
                        this.WindowSize /= 2;
                        if (this.WindowSize < this.session.Config.MinConnectionWindowSize)
                        {
                            this.WindowSize = this.session.Config.MinConnectionWindowSize;
                        }

                        await this.session.resendChan.Writer.WriteAsync(item.Key);
                        var activeChannel = await NcpChannel.SelectChannel(this.session.resendChan, this.session.Context.Done);
                        if (activeChannel == this.session.resendChan)
                        {
                            this.resentSeq.Add(item.Key, default);
                            this.WindowSize /= 2;
                            if (this.WindowSize < this.session.Config.MinConnectionWindowSize)
                            {
                                this.WindowSize = this.session.Config.MinConnectionWindowSize;
                            }
                        }
                        else if (activeChannel == this.session.Context.Done)
                        {
                            throw this.session.Context.Error;
                        }
                    }
                }
            }
        }
    }
}
