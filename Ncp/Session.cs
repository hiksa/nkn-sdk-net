using Ncp.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using System.Threading.Channels;
using Open.ChannelExtensions;
using System.Threading;
using System.Collections.Concurrent;
using Ncp.Exceptions;

namespace Ncp
{
    public class Session
    {
        private readonly string localAddress;
        private readonly string remoteAddress;
        private readonly string[] localClientIds;
        private IList<string> remoteClientIds;
        public Channel<uint?> resendChannel;
        private Channel<uint?> sendChannel;
        private Channel<uint?> sendWindowUpdateChannel;
        private Channel<uint?> recieveDataUpdateChannel;
        private readonly Channel<uint?> onAcceptChannel;
        private IDictionary<uint, byte[]> sendWindowData;
        private ConcurrentDictionary<uint, byte[]> recieveWindowData;
        private byte[] sendBuffer;
        private uint sendMtu;
        private uint sendWindowEndSeq;
        private uint sendWindowSize;
        private uint sendWindowStartSeq;
        private readonly uint recieveWindowSize;
        private readonly uint recieveMtu;
        private uint recieveWindowStartSeq;
        private int recieveWindowUsed;
        private ulong bytesWrite;
        private DateTime bytesReadUpdateTime;
        private ulong remoteBytesRead;
        private Context readContext;
        private Context writeContext;
        private bool isEstablished;
        private IDictionary<string, Connection> connections;
        private bool isAccepted;

        public Session(
            string localAddress,
            string remoteAddress,
            string[] localClientIds,
            List<string> remoteClientIds,
            Func<string, string, byte[], Task> sendWithAsync,
            SessionConfiguration config)
        {
            this.Config = config;

            this.localAddress = localAddress;
            this.remoteAddress = remoteAddress;
            this.localClientIds = localClientIds;
            this.remoteClientIds = remoteClientIds;

            this.SendWithAsync = sendWithAsync;

            this.sendWindowSize = (uint)this.Config.SessionWindowSize;
            this.recieveWindowSize = (uint)this.Config.SessionWindowSize;

            this.sendMtu = (uint)this.Config.Mtu;
            this.recieveMtu = (uint)this.Config.Mtu;

            this.sendWindowStartSeq = Constants.MinSequenceId;
            this.sendWindowEndSeq = Constants.MinSequenceId;
            this.recieveWindowStartSeq = Constants.MinSequenceId;

            this.recieveWindowUsed = 0;
            this.bytesWrite = 0;
            this.BytesRead = 0;
            this.BytesReadSentTime = DateTime.Now;
            this.bytesReadUpdateTime = DateTime.Now;
            this.remoteBytesRead = 0;

            this.onAcceptChannel = Channel.CreateBounded<uint?>(1);
            this.Context = Context.WithCancel(null);

            this.SetTimeout(0);
        }

        public SessionConfiguration Config { get; }

        public DateTime BytesReadSentTime { get; set; }

        public uint BytesRead { get; private set; }

        public Context Context { get; }

        public Func<string, string, byte[], Task> SendWithAsync { get; }

        public bool IsClosed { get; private set; }

        private bool IsStream => this.Config.NonStream == false;

        public static string GetKey(string remoteAddress, string sessionId)
            => remoteAddress + sessionId;

        public static uint NextSequenceId(uint sequenceId, int step)
        {
            var max = uint.MaxValue - Constants.MinSequenceId + 1;
            var result = (sequenceId - Constants.MinSequenceId + step) % max;
            if (result < 0)
            {
                result += max;
            }

            return (uint)(result + Constants.MinSequenceId);
        }

        public static bool IsSequenceInbetween(uint start, uint end, uint target)
        {
            if (start <= end)
            {
                return target >= start && target < end;
            }

            return target >= start || target < end;
        }

        public static int CompareSequenceIds(uint sequenceId1, uint sequenceId2)
        {
            if (sequenceId1 == sequenceId2)
            {
                return 0;
            }

            if (sequenceId1 < sequenceId2)
            {
                if (sequenceId2 - sequenceId1 < uint.MaxValue / 2)
                {
                    return -1;
                }

                return 1;
            }

            if (sequenceId1 - sequenceId2 < uint.MaxValue / 2)
            {
                return 1;
            }

            return -1;
        }

        private ulong GetSendWindowUsed()
        {
            if (this.bytesWrite > this.remoteBytesRead)
            {
                return this.bytesWrite - this.remoteBytesRead;
            }

            return 0;
        }

        public byte[] GetDataToSend(uint sequenceId) => this.sendWindowData[sequenceId];

        public int GetConnWindowSize()
        {
            var windowSize = 0;
            foreach (var connection in this.connections.Values)
            {
                windowSize += connection.WindowSize;
            }

            return windowSize;
        }

        public async Task<uint> GetResendSequenceAsync()
        {
            var cts = new CancellationTokenSource();
            var tasks = new List<Task<uint?>>
            {
                this.resendChannel.ShiftValue(cts.Token),
                this.Context.Done.ShiftValue(cts.Token),
                Constants.ClosedChannel.ShiftValue(cts.Token)
            };

            var value = await tasks.FirstValueAsync(cts);
            if (value == null)
            {
                if (this.Context.Error != null)
                {
                    throw this.Context.Error;
                }

                return 0;
            }

            return value.Value;
        }

        public async Task<uint?> GetSendSequenceAsync()
        {
            var cts = new CancellationTokenSource();
            var tasks = new List<Task<uint?>>
            {
                this.resendChannel.ShiftValue(cts.Token),
                this.sendChannel.ShiftValue(cts.Token),
                this.Context.Done.ShiftValue(cts.Token)
            };

            var value = await tasks.FirstValueAsync(cts);
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
                throw new SessionClosedException();
            }

            var packet = ProtoSerializer.Deserialize<Packet>(buffer);

            if (packet.Data != null)
            {
                Console.WriteLine($"Receiving session message. Packet Id: {packet.SequenceId}, Data Lengtht: {packet.Data?.Length}, Data: {(packet.Data == null ? string.Empty : string.Join(", ", packet.Data.Take(10)))}");
            }

            if (packet.IsClosed)
            {
                this.HandleClosePacket();
                return;
            }

            var established = this.isEstablished;
            if (established == false && packet.IsHandshake)
            {
                this.HandleHandshakePacketAsync(packet);
                return;
            }

            if (established && (packet.AckStartSeqs?.Length > 0 || packet.AckSeqCounts?.Length > 0))
            {
                if (packet.AckSeqCounts?.Length > 0
                    && packet.AckStartSeqs?.Length > 0
                    && packet.AckStartSeqs?.Length != packet.AckSeqCounts?.Length)
                {
                    throw new InvalidPacketException("AckStartSeq and AckSeqCount should have the same length if both are non-empty");
                }

                var count = 0;
                if (packet.AckStartSeqs.Length > 0)
                {
                    count = packet.AckStartSeqs.Length;
                }
                else
                {
                    count = packet.AckSeqCounts.Length;
                }

                uint ackStartSequence;
                uint ackEndSequence;

                for (int i = 0; i < count; i++)
                {
                    if (packet.AckStartSeqs.Length > 0)
                    {
                        ackStartSequence = packet.AckStartSeqs[i];
                    }
                    else
                    {
                        ackStartSequence = Constants.MinSequenceId;
                    }

                    if (packet.AckSeqCounts?.Length > 0)
                    {
                        var step = (int)packet.AckSeqCounts[i];
                        ackEndSequence = Session.NextSequenceId(ackStartSequence, step);
                    }
                    else
                    {
                        ackEndSequence = Session.NextSequenceId(ackStartSequence, 1);
                    }

                    var sequenceInBetween = IsSequenceInbetween(
                        this.sendWindowStartSeq,
                        this.sendWindowEndSeq,
                        Session.NextSequenceId(ackEndSequence, -1));

                    if (sequenceInBetween)
                    {
                        if (IsSequenceInbetween(this.sendWindowStartSeq, this.sendWindowEndSeq, ackStartSequence) == false)
                        {
                            ackStartSequence = this.sendWindowStartSeq;
                        }

                        for (
                            var seq = ackStartSequence;
                            IsSequenceInbetween(ackStartSequence, ackEndSequence, seq);
                            seq = Session.NextSequenceId(seq, 1))
                        {
                            foreach (var connection in this.connections)
                            {
                                var isSentByMe = connection.Key == Connection.GetKey(localClientId, remoteClientId);
                                connection.Value.ReceiveAckAsync(seq, isSentByMe);
                            }

                            this.sendWindowData.Remove(seq);
                        }

                        if (ackStartSequence == this.sendWindowStartSeq)
                        {
                            while (true)
                            {
                                this.sendWindowStartSeq = NextSequenceId(this.sendWindowStartSeq, 1);

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
                this.remoteBytesRead = packet.BytesRead;

                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    this.sendWindowUpdateChannel.Push(null, cts.Token),
                    Constants.ClosedChannel.Shift(cts.Token)
                };

                await channelTasks.FirstAsync(cts);
            }

            if (established && packet.SequenceId > 0)
            {
                if (packet.Data.Length > this.recieveMtu)
                {
                    throw new DataSizeTooLargeException();
                }

                if (CompareSequenceIds(packet.SequenceId, this.recieveWindowStartSeq) >= 0)
                {
                    if (this.recieveWindowData.ContainsKey(packet.SequenceId) == false)
                    {
                        if (this.recieveWindowUsed + packet.Data.Length > this.recieveWindowSize)
                        {
                            throw new RecieveWindowFullException();
                        }

                        this.recieveWindowData.TryAdd(packet.SequenceId, packet.Data);
                        this.recieveWindowUsed += packet.Data.Length;

                        if (packet.SequenceId == this.recieveWindowStartSeq)
                        {
                            var cts2 = new CancellationTokenSource();
                            var channelTasks2 = new List<Task<Channel<uint?>>>
                            {
                                this.recieveDataUpdateChannel.Push(null, cts2.Token),
                                Constants.ClosedChannel.Shift(cts2.Token)
                            };

                            await channelTasks2.FirstAsync(cts2);
                        }
                    }
                }

                var connectionKey = Connection.GetKey(localClientId, remoteClientId);
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
                throw new SessionAlreadyEstablishedException();
            }

            await this.SendHandshakePacketAsync(dialTimeout);

            Channel<uint?> timeout = null;
            var tasks = new List<Task<Channel<uint?>>>();
            var cts = new CancellationTokenSource();

            tasks.Add(this.onAcceptChannel.Shift(cts.Token));

            if (dialTimeout > 0)
            {
                timeout = dialTimeout.ToTimeoutChannel();
                tasks.Add(timeout.Shift(cts.Token));
            }

            var channel = await tasks.FirstAsync(cts);
            if (channel == timeout)
            {
                throw new DialTimeoutException();
            }

            this.Start();

            this.isAccepted = true;
        }

        public async Task AcceptAsync()
        {
            if (this.isAccepted)
            {
                throw new SessionAlreadyEstablishedException();
            }

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.onAcceptChannel.Shift(cts.Token),
                Constants.ClosedChannel.Shift(cts.Token)
            };

            var channel = await channelTasks.FirstAsync(cts);
            if (channel != this.onAcceptChannel)
            {
                throw new MissingHandshakeException();
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
                    throw new SessionClosedException();
                }

                if (this.isEstablished == false)
                {
                    throw new SessionNotEstablishedException();
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

                    var timeout = Constants.MaximumWaitTime.ToTimeoutChannel();
                    var cts = new CancellationTokenSource();
                    var channelTasks = new List<Task<Channel<uint?>>>
                    {
                        this.recieveDataUpdateChannel.Shift(cts.Token),
                        timeout.Shift(cts.Token),
                        this.readContext.Done.Shift(cts.Token)
                    };

                    var channel = await channelTasks.FirstAsync(cts);
                    if (channel == this.readContext.Done)
                    {
                        throw this.readContext.Error;
                    }
                }

                byte[] buffer = default;
                int bytesReceived = 0;

                var data = this.recieveWindowData[this.recieveWindowStartSeq];
                if (this.IsStream == false && maxSize > 0 && maxSize < data.Length)
                {
                    throw new BufferSizeTooSmallException();
                }

                buffer = data.Concat(Enumerable.Empty<byte>()).ToArray();
                bytesReceived = data.Length;
                if (maxSize > 0)
                {
                    buffer = new byte[maxSize];
                    var length = maxSize > data.Length ? data.Length : maxSize;
                    Array.Copy(data, buffer, length);
                    bytesReceived = length;
                }

                if (bytesReceived == data.Length)
                {
                    this.recieveWindowData.TryRemove(this.recieveWindowStartSeq, out _);
                    this.recieveWindowStartSeq = Session.NextSequenceId(this.recieveWindowStartSeq, 1);
                }
                else
                {
                    var subarray = data.Skip(bytesReceived).ToArray();
                    if (this.recieveWindowData.ContainsKey(this.recieveWindowStartSeq))
                    {
                        this.recieveWindowData[this.recieveWindowStartSeq] = subarray;
                    }
                    else
                    {
                        this.recieveWindowData.TryAdd(this.recieveWindowStartSeq, subarray);
                    }
                }

                this.recieveWindowUsed -= bytesReceived;
                this.BytesRead += (uint)bytesReceived;
                this.bytesReadUpdateTime = DateTime.Now;

                if (this.IsStream)
                {
                    while (maxSize < 0 || bytesReceived < maxSize)
                    {
                        if (!this.recieveWindowData.ContainsKey(this.recieveWindowStartSeq))
                        {
                            break;
                        }

                        data = this.recieveWindowData[this.recieveWindowStartSeq];

                        int n;

                        if (maxSize > 0)
                        {
                            var length = maxSize > data.Length ? data.Length : maxSize;
                            Array.Copy(data, 0, buffer, bytesReceived, length);
                            n = length;
                        }
                        else
                        {
                            buffer = buffer.Concat(data).ToArray();
                            n = data.Length;
                        }

                        if (n == data.Length)
                        {
                            this.recieveWindowData.TryRemove(this.recieveWindowStartSeq, out _);
                            this.recieveWindowStartSeq = Session.NextSequenceId(this.recieveWindowStartSeq, 1);
                        }
                        else
                        {
                            this.recieveWindowData.TryAdd(this.recieveWindowStartSeq, data.Skip(n).ToArray());
                        }

                        this.recieveWindowUsed -= n;
                        this.BytesRead += (uint)n;
                        this.bytesReadUpdateTime = DateTime.Now;

                        bytesReceived += n;
                    }

                }
                return buffer.Take(bytesReceived).ToArray();
            }
            catch (ContextExpiredException)
            {
                throw new ReadDeadlineExceededException();
            }
            catch (ContextCanceledException)
            {
                throw new SessionClosedException();
            }
        }

        public async Task WriteAsync(byte[] data)
        {
            try
            {
                if (this.IsClosed)
                {
                    throw new SessionClosedException();
                }

                if (this.isEstablished == false)
                {
                    throw new SessionNotEstablishedException();
                }

                if (this.IsStream == false && (data.Length > this.sendMtu || data.Length > this.sendWindowSize))
                {
                    throw new DataSizeTooLargeException();
                }

                if (data.Length == 0)
                {
                    return;
                }

                uint sentBytesCount = 0;
                if (this.IsStream)
                {
                    while (data.Length > 0)
                    {
                        var sendWindowAvailable = await this.WaitForSendWindowAsync(this.writeContext, 1);

                        uint bytesToSend = (uint)data.Length;
                        if (bytesToSend > sendWindowAvailable)
                        {
                            bytesToSend = sendWindowAvailable;
                        }

                        var shouldFlush = sendWindowAvailable == this.sendWindowSize;
                        var capacity = this.sendMtu;
                        var length = this.sendBuffer.Length;
                        if (bytesToSend >= capacity - length)
                        {
                            bytesToSend = (uint)(capacity - length);
                            shouldFlush = true;
                        }

                        var concatenated = this.sendBuffer.Concat(data.Take((int)bytesToSend)).ToArray();

                        this.sendBuffer = concatenated;

                        this.bytesWrite += bytesToSend;
                        sentBytesCount += bytesToSend;

                        if (shouldFlush)
                        {
                            await this.FlushSendBufferAsync();
                        }

                        data = data.Skip((int)bytesToSend).ToArray();
                    }
                }
                else
                {
                    await this.WaitForSendWindowAsync(this.writeContext, data.Length);

                    this.sendBuffer = data.ToArray();
                    this.bytesWrite += (ulong)data.Length;
                    sentBytesCount += (uint)data.Length;

                    await this.FlushSendBufferAsync();
                }
            }
            catch (ContextExpiredException)
            {
                throw new WriteDeadlineExceededException();
            }
            catch (ContextCanceledException)
            {
                throw new SessionClosedException();
            }
        }

        private void Start()
        {
            Task.Run(this.StartFlushAsync);
            Task.Run(this.StartCheckBytesReadAsync);

            foreach (var connection in this.connections.Values)
            {
                connection.Start();
            }
        }

        private async Task StartFlushAsync()
        {
            while (true)
            {
                var timeout = this.Config.FlushInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    timeout.Shift(cts.Token),
                    this.Context.Done.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.Context.Done)
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
                var timeout = this.Config.CheckBytesReadInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    timeout.Shift(cts.Token),
                    this.Context.Done.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
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
                    var tasks = new Task[this.connections.Count];
                    var i = 0;
                    foreach (var connection in this.connections.Values)
                    {
                        tasks[i] = Task.Run(async () => await this.SendWithAsync(connection.LocalClientId, connection.RemoteClientId, buffer));
                        i++;
                    }

                    await Task.WhenAny(tasks);
                    this.BytesReadSentTime = DateTime.Now;
                }
                catch (Exception)
                {
                    await Task.Delay(1000);
                    continue;
                }
            }
        }

        private async Task<uint> WaitForSendWindowAsync(Context context, int n)
        {
            var sendWindowUsed = (uint)this.GetSendWindowUsed();
            while (sendWindowUsed + (ulong)n > this.sendWindowSize)
            {
                var timeout = Constants.MaximumWaitTime.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    this.sendWindowUpdateChannel.Shift(cts.Token),
                    timeout.Shift(cts.Token),
                    context.Done.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == context.Done)
                {
                    throw context.Error;
                }

                sendWindowUsed = (uint)this.GetSendWindowUsed();
            }

            return this.sendWindowSize - sendWindowUsed;
        }

        private async Task FlushSendBufferAsync()
        {
            uint sequenceId = 0;

            lock (this)
            {
                if (this.sendBuffer == null || this.sendBuffer.Length == 0)
                {
                    return;
                }

                sequenceId = this.sendWindowEndSeq;
                var packet = new Packet
                {
                    SequenceId = sequenceId,
                    Data = this.sendBuffer
                };

           //     Console.WriteLine($"Queueing to send session data. Packet Id: {packet.SequenceId}, Data Length: {packet.Data?.Length}, Data: {(packet.Data == null ? string.Empty : string.Join(", ", packet.Data.Take(10)))}");
                var buffer = ProtoSerializer.Serialize(packet);

                this.sendWindowData.Add(sequenceId, buffer);

                this.sendWindowEndSeq = Session.NextSequenceId(sequenceId, 1);
                this.sendBuffer = new byte[0];
            }

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.sendChannel.Push(sequenceId, cts.Token),
                this.Context.Done.Shift(cts.Token)
            };

            var channel = await channelTasks.FirstAsync(cts);
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
                ClientIds = this.localClientIds.ToList(),
                WindowSize = this.recieveWindowSize,
                Mtu = this.recieveMtu
            };

            var data = ProtoSerializer.Serialize(packet);

            var tasks = new List<Task>();
            if (this.connections != null && this.connections.Count > 0)
            {
                foreach (var connection in this.connections.Values)
                {
                    var task = this.SendWithAsync(connection.LocalClientId, connection.RemoteClientId, data);

                    tasks.Add(task.ToTimeoutTask(timeout, new WriteDeadlineExceededException()));
                }
            }
            else
            {
                for (int i = 0; i < this.localClientIds.Length; i++)
                {
                    var remoteClientId = this.localClientIds[i];
                    if (this.remoteClientIds != null && this.remoteClientIds.Count > 0)
                    {
                        remoteClientId = this.remoteClientIds[i % this.remoteClientIds.Count];
                    }

                    var task = this.SendWithAsync(this.localClientIds[i], remoteClientId, data);

                    tasks.Add(task.ToTimeoutTask(timeout, new WriteDeadlineExceededException()));
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

        private void HandleHandshakePacketAsync(Packet packet)
        {
            if (this.isEstablished)
            {
                return;
            }

            if (packet.WindowSize == 0)
            {
                throw new InvalidPacketException("WindowSize is zero");
            }

            if (packet.WindowSize < this.sendWindowSize)
            {
                this.sendWindowSize = packet.WindowSize;
            }

            if (packet.Mtu == 0)
            {
                throw new InvalidPacketException("MTU is zero");
            }

            if (packet.Mtu < this.sendMtu)
            {
                this.sendMtu = packet.Mtu;
            }

            if (packet.ClientIds.Count == 0)
            {
                throw new InvalidPacketException("ClientIDs is empty");
            }

            var connectionsCount = this.localClientIds.Count();
            if (packet.ClientIds.Count < connectionsCount)
            {
                connectionsCount = packet.ClientIds.Count;
            }

            IDictionary<string, Connection> connections = new ConcurrentDictionary<string, Connection>();
            for (int i = 0; i < connectionsCount; i++)
            {
                var connection = new Connection(this, this.localClientIds[i], packet.ClientIds[i]);
                var connectionKey = Connection.GetKey(connection.LocalClientId, connection.RemoteClientId);
                connections.Add(connectionKey, connection);
            }

            this.connections = connections;

            this.remoteClientIds = packet.ClientIds;
            this.sendChannel = Channel.CreateUnbounded<uint?>();
            this.resendChannel = Channel.CreateBounded<uint?>(this.Config.MaxConnectionWindowSize * connectionsCount);
            this.sendWindowUpdateChannel = Channel.CreateBounded<uint?>(1);
            this.recieveDataUpdateChannel = Channel.CreateBounded<uint?>(1);

            this.sendBuffer = new byte[0];
            this.sendWindowData = new ConcurrentDictionary<uint, byte[]>();
            this.recieveWindowData = new ConcurrentDictionary<uint, byte[]>();
            this.isEstablished = true;

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.onAcceptChannel.Push(null, cts.Token),
                Constants.ClosedChannel.Shift(cts.Token)
            };

            channelTasks.FirstAsync(cts);
        }

        private async Task SendClosePacketAsync()
        {
            if (this.isEstablished == false)
            {
                throw new SessionNotEstablishedException();
            }

            var packet = new Packet { IsClosed = true };
            var buffer = ProtoSerializer.Serialize(packet);

            var tasks = new Task[this.connections.Count];
            var i = 0;
            foreach (var connection in this.connections.Values)
            {
                var task = this.SendWithAsync(connection.LocalClientId, connection.RemoteClientId, buffer);

                tasks[i] = task.ToTimeoutTask(connection.RetransmissionTimeout, new WriteDeadlineExceededException());
            }

            try
            {
                await Task.WhenAny(tasks);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void HandleClosePacket()
        {
            Task.Run(this.readContext.CancelAsync);
            Task.Run(this.writeContext.CancelAsync);
            Task.Run(this.Context.CancelAsync);

            this.IsClosed = true;
        }

        public async Task CloseAsync()
        {
            await this.readContext.CancelAsync();
            await this.writeContext.CancelAsync();

            var timeout = Channel.CreateBounded<uint?>(1);

            if (this.Config.Linger > 0)
            {
                Task.Run(async delegate
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

                await Task.Run(async delegate
                {
                    while (true)
                    {
                        var interval = 100.ToTimeoutChannel();
                        var cts = new CancellationTokenSource();
                        var channelTasks = new List<Task<Channel<uint?>>>
                        {
                            interval.Shift(cts.Token),
                            timeout.Shift(cts.Token)
                        };

                        var channel = await channelTasks.FirstAsync(cts);

                        if (channel == interval)
                        {
                            if (this.sendWindowStartSeq == this.sendWindowEndSeq)
                            {
                                return;
                            }
                        }
                        else if (channel == timeout)
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
                Console.WriteLine(e.Message);
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

        public void SetLinger(int linger) => this.Config.Linger = linger;
    }
}
