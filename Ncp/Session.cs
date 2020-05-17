using Ncp.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using System.Threading.Channels;
using Open.ChannelExtensions;
using System.Threading;

namespace Ncp
{
    public class Session
    {
        private readonly string localAddress;
        private readonly string remoteAddress;
        private readonly string[] localClientIds;
        private string[] remoteClientIds;
        private Channel<int?> sendChan;
        public Channel<int?> resendChan;
        private Channel<int?> sendWindowUpdate;
        private Channel<int?> recieveDataUpdate;
        private byte[] sendBuffer;
        private Dictionary<int, byte[]> sendWindowData;
        private Dictionary<int, byte[]> recieveWindowData;
        private int sendWindowSize;
        private readonly int recieveWindowSize;
        private int sendMtu;
        private readonly int recieveMtu;
        private int sendWindowStartSeq;
        private int sendWindowEndSeq;
        private int recieveWindowStartSeq;
        private int recvWindowUsed;
        private int recieveWindowUsed;
        private int bytesWrite;
        private DateTime bytesReadUpdateTime;
        private int remoteBytesRead;
        private readonly Channel<int?> onAccept;
        private Context readContext;
        private Context writeContext;
        private bool isEstablished;
        private IDictionary<string, Connection> connections;
        private bool isAccepted;

        public Session(
            string localAddress, 
            string remoteAddress, 
            string[] localClientIds, 
            string[] remoteClientIds, 
            Func<string, string, byte[], Task> sendWith, 
            SessionConfiguration config)
        {
            this.Config = config;

            this.localAddress = localAddress;
            this.remoteAddress = remoteAddress;
            this.localClientIds = localClientIds;
            this.remoteClientIds = remoteClientIds;

            this.SendWith = sendWith;

            this.sendWindowSize = this.Config.SessionWindowSize;
            this.recieveWindowSize = this.Config.SessionWindowSize;

            this.sendMtu = this.Config.Mtu;
            this.recieveMtu = this.Config.Mtu;

            this.sendWindowStartSeq = Constants.MinSequenceId;
            this.sendWindowEndSeq = Constants.MinSequenceId;
            this.recieveWindowStartSeq = Constants.MinSequenceId;

            this.recieveWindowUsed = 0;
            this.bytesWrite = 0;
            this.BytesRead = 0;
            this.BytesReadSentTime = DateTime.Now;
            this.bytesReadUpdateTime = DateTime.Now;
            this.remoteBytesRead = 0;

            this.onAccept = Channel.CreateBounded<int?>(10);
            this.Context = Context.WithCancel(null);

            this.SetTimeout(0);
        }

        public SessionConfiguration Config { get; }

        public DateTime BytesReadSentTime { get; set; }

        public int BytesRead { get; private set; }

        public Context Context { get; }

        public Func<string, string, byte[], Task> SendWith { get; }

        public bool IsClosed { get; private set; }

        public bool IsStream() => this.Config.NonStream == false;

        public int SendWindowUsed()
        {
            if (this.bytesWrite > this.remoteBytesRead)
            {
                return this.bytesWrite - this.remoteBytesRead;
            }

            return 0;
        }

        public byte[] GetDataToSend(int sequenceId) => this.sendWindowData[sequenceId];

        public int GetConnWindowSize()
        {
            var windowSize = 0;
            foreach (var connection in this.connections.Values)
            {
                windowSize += connection.WindowSize;
            }

            return windowSize;
        }

        public async Task<int> GetResendSeq()
        {
            var closed = NcpChannel.ClosedChannel;
            var channel = await NcpChannel.SelectChannel(
                this.resendChan,
                this.Context.Done,
                closed);

            if (channel == this.Context.Done || channel == closed)
            {
                if (this.Context.Error != null)
                {
                    throw this.Context.Error;
                }

                return 0;
            }

            var result = await this.resendChan.Reader.ReadAsync();

            return result.Value;
        }

        public async Task<int?> GetSendSequence()
        {
            var value = await NcpChannel.SelectValue(this.resendChan, this.sendChan, this.Context.Done);
            if (value == null)
            {
                throw this.Context.Error;
            }

            return value;
        }

        public async Task ReceiveWithAsync(string localClientId, string remoteClientId, byte[] buffer)
        {
            if (this.IsClosed)
            {
                throw new Exception();
            }

            var packet = ProtoSerializer.Deserialize<Packet>(buffer);
            if (packet.IsClosed)
            {
                await this.HandleClosePacketAsync();
                return;
            }

            var established = this.isEstablished;
            if (established == false && packet.IsHandshake)
            {
                this.HandleHandshakePacket(packet);
                return;
            }

            if (established && (packet.AckStartSeq?.Length > 0 || packet.AckSeqCount?.Length > 0))
            {
                if (packet.AckSeqCount?.Length > 0
                    && packet.AckStartSeq?.Length > 0
                    && packet.AckStartSeq?.Length != packet.AckSeqCount?.Length)
                {
                    throw new Exception("AckStartSeq and AckSeqCount should have the same length if both are non-empty");
                }

                var count = 0;
                if (packet.AckStartSeq.Length > 0)
                {
                    count = packet.AckStartSeq.Length;
                }
                else
                {
                    count = packet.AckSeqCount.Length;
                }

                var ackStartSeq = 0;
                var ackEndSeq = 0;

                for (int i = 0; i < count; i++)
                {
                    if (packet.AckStartSeq.Length > 0)
                    {
                        ackStartSeq = packet.AckStartSeq[i];
                    }
                    else
                    {
                        ackStartSeq = Constants.MinSequenceId;
                    }

                    if (packet.AckSeqCount?.Length > 0)
                    {
                        var step = packet.AckSeqCount[i];
                        ackEndSeq = Util.NextSequence(ackStartSeq, step);
                    }
                    else
                    {
                        ackEndSeq = Util.NextSequence(ackStartSeq, 1);
                    }

                    var sequenceInBetween = Util.IsSequenceInBetween(this.sendWindowStartSeq, this.sendWindowEndSeq, Util.NextSequence(ackEndSeq, -1));
                    if (sequenceInBetween)
                    {
                        if (Util.IsSequenceInBetween(this.sendWindowStartSeq, this.sendWindowEndSeq, ackStartSeq) == false)
                        {
                            ackStartSeq = this.sendWindowStartSeq;
                        }

                        for (int seq = ackStartSeq; Util.IsSequenceInBetween(ackStartSeq, ackEndSeq, seq); seq = Util.NextSequence(seq, 1))
                        {
                            foreach (var connection in this.connections)
                            {
                                var isSentByMe = connection.Key == Connection.MakeConnectionKey(localClientId, remoteClientId);
                                await connection.Value.ReceiveAck(seq, isSentByMe);
                            }

                            this.sendWindowData.Remove(seq);
                        }

                        if (ackStartSeq == this.sendWindowStartSeq)
                        {
                            while (true)
                            {
                                this.sendWindowStartSeq = Util.NextSequence(this.sendWindowStartSeq, 1);

                                if (this.sendWindowData.ContainsKey(this.sendWindowStartSeq))
                                {
                                    break;
                                }

                                if (this.sendWindowStartSeq == this.sendWindowEndSeq)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (established && packet.BytesRead > this.remoteBytesRead)
            {
                this.remoteBytesRead = (int)packet.BytesRead;

                await this.sendWindowUpdate.Writer.WriteAsync(null);

                await NcpChannel.SelectChannel(this.sendWindowUpdate, NcpChannel.ClosedChannel);
            }

            if (established && packet.SequenceId > 0)
            {
                if (packet.Data.Length > this.recieveMtu)
                {
                    throw new Exception();
                }

                if (Util.CompareSeq(packet.SequenceId, this.recieveWindowStartSeq) >= 0)
                {
                    if (this.recieveWindowData.ContainsKey(packet.SequenceId) == false)
                    {
                        if (this.recvWindowUsed + packet.Data.Length > this.recieveWindowSize)
                        {
                            throw new Exception();
                        }

                        this.recieveWindowData.Add(packet.SequenceId, packet.Data);
                        this.recvWindowUsed += packet.Data.Length;

                        if (packet.SequenceId == this.recieveWindowStartSeq)
                        {
                            await this.recieveDataUpdate.Writer.WriteAsync(null);

                            var closed = NcpChannel.ClosedChannel;

                            await NcpChannel.SelectChannel(this.recieveDataUpdate, closed);
                        }
                    }
                }

                var connectionKey = Connection.MakeConnectionKey(localClientId, remoteClientId);
                if (this.connections.ContainsKey(connectionKey))
                {
                    var conn = this.connections[connectionKey];
                    conn.SendAck(packet.SequenceId);
                }
            }
        }

        public async Task DialAsync(int dialTimeout)
        {
            if (this.isAccepted)
            {
                throw new Exception();
            }

            await this.SendHandshakePacketAsync(dialTimeout);

            Channel<int?> timeout = null;
            var channels = new Channel<int?>[2];
            await this.onAccept.Writer.WriteAsync(null);
            channels[0] = this.onAccept;
            if (dialTimeout > 0)
            {
                timeout = Util.MakeTimeoutChannel(dialTimeout);
                channels[1] = timeout;
            }

            var selected = await NcpChannel.SelectChannel(channels);
            if (selected == timeout)
            {
                throw new Exception();
            }

            this.Start();

            this.isAccepted = true;
        }

        public async Task AcceptAsync()
        {
            if (this.isAccepted)
            {
                throw new Exception();
            }

            await this.onAccept.Writer.WriteAsync(null);
            var activeChannel = await NcpChannel.SelectChannel(this.onAccept, NcpChannel.ClosedChannel);
            if (activeChannel != this.onAccept)
            {
                throw new Exception();
            }

            this.Start();

            this.isAccepted = true;

            await this.SendHandshakePacketAsync(this.Config.MaxRetransmissionTimeout);
        }

        public async Task<byte[]> ReadAsync(int maxSize = 0)
        {
            try
            {
                if (this.IsClosed)
                {
                    throw new Exception();
                }

                if (this.isEstablished == false)
                {
                    throw new Exception();
                }

                while (true)
                {
                    if (this.readContext.Error != null)
                    {
                        throw this.readContext.Error;
                    }

                    if (this.recieveWindowData.ContainsKey(this.recieveWindowStartSeq))
                    {
                        break;
                    }

                    var timeout = Util.MakeTimeoutChannel(Constants.MaximumWaitTime);

                    var activeChannel = await NcpChannel.SelectChannel(this.recieveDataUpdate, timeout, this.readContext.Done);
                    if (activeChannel == this.readContext.Done)
                    {
                        throw this.readContext.Error;
                    }
                }

                var data = this.recieveWindowData[this.recieveWindowStartSeq];
                if (this.IsStream() == false && maxSize > 0 && maxSize < data.Length)
                {
                    throw new Exception();
                }

                var b = data;
                var bytesReceived = data.Length;
                if (maxSize > 0)
                {
                    b = new byte[maxSize];
                    var subArray = data.Take(maxSize).ToArray();
                    Array.Copy(subArray, b, subArray.Length);
                    bytesReceived = subArray.Length;
                }

                if (bytesReceived == data.Length)
                {
                    this.recieveWindowData.Remove(this.recieveWindowStartSeq);
                    this.recieveWindowStartSeq = Util.NextSequence(this.recieveWindowStartSeq, 1);
                }
                else
                {
                    var subarray = data.Take(bytesReceived).ToArray();
                    if (this.recieveWindowData.ContainsKey(this.recieveWindowStartSeq))
                    {
                        this.recieveWindowData[this.recieveWindowStartSeq] = subarray;
                    }
                    else
                    {
                        this.recieveWindowData.Add(this.recieveWindowStartSeq, subarray);
                    }
                }

                this.recieveWindowUsed -= bytesReceived;
                this.BytesRead += bytesReceived;
                this.bytesReadUpdateTime = DateTime.Now;

                if (this.IsStream())
                {
                    while (maxSize < 0 || bytesReceived < maxSize)
                    {
                        data = this.recieveWindowData[this.recieveWindowStartSeq];
                        if (data == null)
                        {
                            break;
                        }

                        var n = 0;

                        if (maxSize > 0)
                        {
                            var subarray = data.Take(maxSize - bytesReceived).ToArray();
                            Array.Copy(subarray, 0, b, bytesReceived, subarray.Length);
                            n = subarray.Length;
                        }
                        else
                        {
                            b = b.Concat(data).ToArray();
                            n = data.Length;
                        }

                        if (n == data.Length)
                        {
                            this.recieveWindowData.Remove(this.recieveWindowStartSeq);
                            this.recieveWindowStartSeq = Util.NextSequence(this.recieveWindowStartSeq, 1);
                        }
                        else
                        {
                            this.recieveWindowData.Add(this.recieveWindowStartSeq, data.Skip(n).ToArray());
                        }

                        this.recieveWindowUsed -= n;
                        this.BytesRead += n;
                        this.bytesReadUpdateTime = DateTime.Now;

                        bytesReceived += n;
                    }
                }

                return b.Take(bytesReceived).ToArray();
            }
            catch (Exception)
            {
                throw new Exception();
            }
        }

        public async Task WriteAsync(byte[] data)
        {
            try
            {
                if (this.IsClosed)
                {
                    throw new Exception();
                }

                if (this.isEstablished == false)
                {
                    throw new Exception();
                }

                if (this.IsStream() == false && (data.Length > this.sendMtu || data.Length > this.sendWindowSize))
                {
                    throw new Exception();
                }

                if (data.Length == 0)
                {
                    return;
                }

                var bytesSent = 0;
                if (this.IsStream())
                {
                    while (data.Length > 0)
                    {
                        var sendWindowAvailable = await this.WaitForSendWindow(this.writeContext, 1);

                        var n = data.Length;
                        if (n > sendWindowAvailable)
                        {
                            n = sendWindowAvailable;
                        }

                        var shouldFlush = sendWindowAvailable == this.sendWindowSize;
                        var c = this.sendMtu;
                        var length = this.sendBuffer.Length;
                        if (n >= c - length)
                        {
                            n = c - length;
                            shouldFlush = true;
                        }

                        this.sendBuffer = this.sendBuffer.Concat(data.Take(n)).ToArray();

                        this.bytesWrite += n;
                        bytesSent += n;

                        if (shouldFlush)
                        {
                            await this.FlushSendBufferAsync();
                        }

                        data = data.Skip(n).ToArray();
                    }
                }
                else
                {
                    await this.WaitForSendWindow(this.writeContext, data.Length);

                    this.sendBuffer = data.ToArray();
                    this.bytesWrite += data.Length;
                    bytesSent += data.Length;

                    await this.FlushSendBufferAsync();
                }
            }
            catch (Exception e)
            {
                throw new Exception();
            }
        }

        private void Start()
        {
            Task.Factory.StartNew(this.StartFlushAsync);
            Task.Factory.StartNew(this.StartCheckBytesReadAsync);

            if (this.connections != null)
            {
                foreach (var connection in this.connections.Values)
                {
                    connection.Start();
                }
            }
        }

        private async Task StartFlushAsync()
        {
            while (true)
            {
                var timeout = Util.MakeTimeoutChannel(this.Config.FlushInterval);
                var firstActive = await NcpChannel.SelectChannel(timeout, this.Context.Done);
                if (firstActive == this.Context.Done)
                {
                    throw this.Context.Error;
                }

                if (this.sendBuffer == null || this.sendBuffer.Length == 0)
                {
                    continue;
                }

                try
                {
                    await this.FlushSendBufferAsync();
                }
                catch (Exception e)
                {
                    if (this.Context.Error != null)
                    {
                        throw e;
                    }

                    continue;
                }
            }
        }

        private async Task StartCheckBytesReadAsync()
        {
            while (true)
            {
                var timeout = Util.MakeTimeoutChannel(this.Config.CheckBytesReadInterval);
                var channel = await NcpChannel.SelectChannel(timeout, this.Context.Done);
                if (channel == this.Context.Done)
                {
                    throw this.Context.Error;
                }

                if (this.BytesRead == 0
                    || this.BytesReadSentTime > this.bytesReadUpdateTime
                    || (DateTime.Now - this.bytesReadUpdateTime).TotalMilliseconds < this.Config.SendBytesReadThreshold)
                {
                    continue;
                }

                try
                {
                    var packet = new Packet
                    {
                        BytesRead = this.BytesRead
                    };

                    var buffer = ProtoSerializer.Serialize(packet);
                    Task[] tasks = new Task[this.connections.Count];
                    var i = 0;
                    foreach (var connection in this.connections.Values)
                    {
                        tasks[i] = Task.Factory.StartNew(() => this.SendWith(connection.LocalClientId, connection.RemoteClientId, buffer));
                        i++;
                    }

                    Task.WaitAny(tasks);
                    this.BytesReadSentTime = DateTime.Now;
                }
                catch (Exception)
                {
                    await Task.Delay(1000);
                    continue;
                }
            }
        }

        private async Task<int> WaitForSendWindow(Context context, int n)
        {
            while (this.SendWindowUsed() + n > this.sendWindowSize)
            {
                var timeout = Util.MakeTimeoutChannel(Constants.MaximumWaitTime);
                var channel = await NcpChannel.SelectChannel(
                    this.sendWindowUpdate,
                    timeout,
                    context.Done);

                if (channel == context.Done)
                {
                    throw context.Error;
                }
            }

            return this.sendWindowSize - this.SendWindowUsed();
        }

        private async Task FlushSendBufferAsync()
        {
            if (this.sendBuffer == null || this.sendBuffer.Length == 0)
            {
                return;
            }

            var sequenceId = this.sendWindowEndSeq;
            var packet = new Packet
            {
                SequenceId = sequenceId,
                Data = this.sendBuffer
            };

            var buffer = ProtoSerializer.Serialize(packet);

            this.sendWindowData.Add(sequenceId, buffer);
            this.sendWindowEndSeq = Util.NextSequence(sequenceId, 1);
            this.sendBuffer = new byte[0];

            await this.sendChan.Writer.WriteAsync(sequenceId);

            var channel = await NcpChannel.SelectChannel(this.sendChan, this.Context.Done);
            if (channel == this.Context.Done)
            {
                throw this.Context.Error;
            }
        }

        private async Task SendHandshakePacketAsync(int timeout)
        {
            var packet = new Packet
            {
                IsHandshake = true,
                ClientIds = this.localClientIds,
                WindowSize = this.recieveWindowSize,
                Mtu = this.recieveMtu
            };

            var buffer = ProtoSerializer.Serialize(packet);

            var tasks = new List<Task>();
            if (this.connections != null && this.connections.Count > 0)
            {
                foreach (var connection in this.connections.Values)
                {
                    var task = await Util.MakeTimeoutTaskAsync(
                        this.SendWith(connection.LocalClientId, connection.RemoteClientId, buffer),
                        timeout);
                    tasks.Add(task);
                }
            }
            else
            {
                for (int i = 0; i < this.localClientIds.Length; i++)
                {
                    var remoteClientId = this.localClientIds[i];
                    if (this.remoteClientIds != null && this.remoteClientIds.Length > 0)
                    {
                        remoteClientId = this.remoteClientIds[i % this.remoteClientIds.Length];
                    }

                    var task = Util.MakeTimeoutTaskAsync(
                        this.SendWith(this.localClientIds[i], remoteClientId, buffer),
                        timeout);
                    tasks.Add(task);
                }
            }

            try
            {
                await Task.WhenAny(tasks.ToArray());
            }
            catch (Exception e)
            {
            }
        }

        private void HandleHandshakePacket(Packet packet)
        {
            if (this.isEstablished)
            {
                return;
            }

            if (packet.WindowSize == 0)
            {
                throw new Exception();
            }

            if (packet.WindowSize < this.sendWindowSize)
            {
                this.sendWindowSize = packet.WindowSize;
            }

            if (packet.Mtu == 0)
            {
                throw new Exception();
            }

            if (packet.Mtu < this.sendMtu)
            {
                this.sendMtu = packet.Mtu;
            }

            if (packet.ClientIds.Length == 0)
            {
                throw new Exception();
            }

            var targetConnectionCount = this.localClientIds.Count();
            if (packet.ClientIds.Length < targetConnectionCount)
            {
                targetConnectionCount = packet.ClientIds.Length;
            }

            var connections = new Dictionary<string, Connection>();
            for (int i = 0; i < targetConnectionCount; i++)
            {
                var connection = new Connection(this, this.localClientIds[i], packet.ClientIds[i]);
                connections.Add(connection.Key, connection);
            }

            this.connections = connections;

            this.remoteClientIds = packet.ClientIds;
            this.sendChan = Channel.CreateBounded<int?>(50);
            this.resendChan = Channel.CreateBounded<int?>(this.Config.MaxConnectionWindowSize * targetConnectionCount);
            this.sendWindowUpdate = Channel.CreateBounded<int?>(50);
            this.recieveDataUpdate = Channel.CreateBounded<int?>(50);

            this.sendBuffer = new byte[0];
            this.sendWindowData = new Dictionary<int, byte[]>();
            this.recieveWindowData = new Dictionary<int, byte[]>();
            this.isEstablished = true;

            this.onAccept.Writer.WriteAsync(null);
        }

        private async Task SendClosePacketAsync()
        {
            if (this.isEstablished == false)
            {
                throw new Exception();
            }

            var packet = new Packet { IsClosed = true };
            var buffer = ProtoSerializer.Serialize(packet);

            var tasks = new Task[this.connections.Count];
            var i = 0;
            foreach (var connection in this.connections.Values)
            {
                var task = await Util.MakeTimeoutTaskAsync(
                    this.SendWith(connection.LocalClientId, connection.RemoteClientId, buffer), 
                    connection.RetransmissionTimeout);
                tasks[i] = task;
            }

            try
            {
                Task.WaitAny(tasks);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private async Task HandleClosePacketAsync()
        {
            await this.readContext.CancelAsync();
            await this.writeContext.CancelAsync();
            await this.Context.CancelAsync();

            this.IsClosed = true;
        }

        public async Task CloseAsync()
        {
            await this.readContext.CancelAsync();
            await this.writeContext.CancelAsync();

            var timeout = Channel.CreateBounded<int?>(1);

            if (this.Config.Linger > 0)
            {
                await Task.Factory.StartNew(async delegate
                {
                    await Task.Delay(this.Config.Linger);
                    await timeout.CompleteAsync();
                });
            }

            if (this.Config.Linger != 0)
            {
                try
                {
                    await this.FlushSendBufferAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                await Task.Factory.StartNew(async delegate
                {
                    while (true)
                    {
                        var interval = Util.MakeTimeoutChannel(100);
                        var selected = await NcpChannel.SelectChannel(interval, timeout);
                        if (selected == interval)
                        {
                            if (this.sendWindowStartSeq == this.sendWindowEndSeq)
                            {
                                return;
                            }
                        }
                        else if (selected == timeout)
                        {
                            return;
                        }
                    }
                });
            }

            try
            {
                await this.SendClosePacketAsync();
            }
            catch (Exception e)
            {
            }

            await this.Context.CancelAsync();

            this.IsClosed = true;
        }

        private void SetTimeout(int timeout)
        {
            this.SetReadTimeout(timeout);
            this.SetWriteTimeout(timeout);
        }

        private void SetWriteTimeout(int timeout)
            => this.writeContext = Context.WithTimeout(this.Context, timeout);

        private void SetReadTimeout(int timeout)
            => this.readContext = Context.WithTimeout(this.Context, timeout);

        private void SetLinger(int linger) => this.Config.Linger = linger;
    }
}
