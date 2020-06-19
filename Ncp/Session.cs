using Ncp.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;
using System.Collections.Concurrent;
using Ncp.Exceptions;
using Open.ChannelExtensions;

namespace Ncp
{
    public class Session
    {
        private const int DefaultStepSize = 1;

        private readonly object syncLock = new object();
        private readonly string[] localClientIds;
        private IList<string> remoteClientIds;
        private Channel<uint?> sendChannel;
        private Channel<uint?> sendWindowUpdateChannel;
        private Channel<uint?> recieveDataUpdateChannel;
        private readonly Channel<uint?> onAcceptChannel;
        private IDictionary<uint, byte[]> sendWindowData;
        private ConcurrentDictionary<uint, byte[]> receiveWindowData;
        private byte[] sendBuffer;
        private uint sendMtu;
        private uint sendWindowEndSequenceId;
        private uint sendWindowSize;
        private uint sendWindowStartSequenceId;
        private readonly uint receiveWindowSize;
        private readonly uint recieveMtu;
        private uint receiveWindowStartSeq;
        private int receiveWindowUsed;
        private ulong totalBytesWritten;
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
            Func<string, string, byte[], Task> sendSessionDataHandler,
            SessionOptions options)
        {
            this.Options = options;

            this.LocalAddress = localAddress;
            this.RemoteAddress = remoteAddress;
            this.localClientIds = localClientIds;
            this.remoteClientIds = remoteClientIds;

            this.SendDataAsync = sendSessionDataHandler;

            this.sendWindowSize = (uint)this.Options.SessionWindowSize;
            this.receiveWindowSize = (uint)this.Options.SessionWindowSize;

            this.sendMtu = (uint)this.Options.Mtu;
            this.recieveMtu = (uint)this.Options.Mtu;

            this.sendWindowStartSequenceId = Constants.MinSequenceId;
            this.sendWindowEndSequenceId = Constants.MinSequenceId;
            this.receiveWindowStartSeq = Constants.MinSequenceId;

            this.receiveWindowUsed = 0;
            this.totalBytesWritten = 0;
            this.TotalBytesRead = 0;
            this.AckSentTime = DateTime.Now;
            this.bytesReadUpdateTime = DateTime.Now;
            this.remoteBytesRead = 0;

            this.onAcceptChannel = Channel.CreateBounded<uint?>(1);
            this.Context = Context.WithCancel(null);

            this.SetTimeout(0);
        }

        public SessionOptions Options { get; }

        public Context Context { get; }

        public string LocalAddress { get; }

        public string RemoteAddress { get; }

        public Func<string, string, byte[], Task> SendDataAsync { get; }

        public Channel<uint?> ResendChannel { get; private set; }

        public uint TotalBytesRead { get; private set; }

        public bool IsClosed { get; private set; }

        public DateTime AckSentTime { get; set; }

        private bool IsStream => this.Options.NonStream == false;

        public static string MakeKey(string remoteAddress, string sessionId) => remoteAddress + sessionId;

        public static uint NextSequenceId(uint sequenceId, int step = Session.DefaultStepSize)
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

        public void SetLinger(int linger) => this.Options.Linger = linger;

        public async Task<uint> GetResendSequenceAsync()
        {
            var cts = new CancellationTokenSource();
            var tasks = new List<Task<uint?>>
            {
                this.ResendChannel.ShiftValue(cts.Token),
                this.Context.DoneChannel.ShiftValue(cts.Token),
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
                this.ResendChannel.ShiftValue(cts.Token),
                this.sendChannel.ShiftValue(cts.Token),
                this.Context.DoneChannel.ShiftValue(cts.Token)
            };

            var value = await tasks.FirstValueAsync(cts);
            if (value == null)
            {
                throw this.Context.Error;
            }

            return value;
        }

        public async Task ReceiveWithClientAsync(string localClientId, string remoteClientId, byte[] buffer)
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

            if (packet.IsClose)
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

                int count;
                if (packet.AckStartSeqs.Length > 0)
                {
                    count = packet.AckStartSeqs.Length;
                }
                else
                {
                    count = packet.AckSeqCounts.Length;
                }

                uint ackStartSequenceId;
                uint ackEndSequenceId;

                for (int i = 0; i < count; i++)
                {
                    if (packet.AckStartSeqs.Length > 0)
                    {
                        ackStartSequenceId = packet.AckStartSeqs[i];
                    }
                    else
                    {
                        ackStartSequenceId = Constants.MinSequenceId;
                    }

                    if (packet.AckSeqCounts?.Length > 0)
                    {
                        var step = (int)packet.AckSeqCounts[i];
                        ackEndSequenceId = Session.NextSequenceId(ackStartSequenceId, step);
                    }
                    else
                    {
                        ackEndSequenceId = Session.NextSequenceId(ackStartSequenceId);
                    }

                    var nextSequenceAvailable = Session.IsSequenceInbetween(
                        this.sendWindowStartSequenceId,
                        this.sendWindowEndSequenceId,
                        Session.NextSequenceId(ackEndSequenceId, -Session.DefaultStepSize));

                    if (nextSequenceAvailable)
                    {
                        var ackIsInBounds = Session.IsSequenceInbetween(
                            this.sendWindowStartSequenceId,
                            this.sendWindowEndSequenceId,
                            ackStartSequenceId);

                        if (ackIsInBounds == false)
                        {
                            ackStartSequenceId = this.sendWindowStartSequenceId;
                        }

                        for (
                            var currentSequenceId = ackStartSequenceId;
                            Session.IsSequenceInbetween(ackStartSequenceId, ackEndSequenceId, currentSequenceId);
                            currentSequenceId = Session.NextSequenceId(currentSequenceId))
                        {
                            foreach (var connection in this.connections)
                            {
                                var isSentByMe = connection.Key == Connection.GetKey(localClientId, remoteClientId);

                                connection.Value.ReceiveAck(currentSequenceId, isSentByMe);
                            }

                            this.sendWindowData.Remove(currentSequenceId);
                        }

                        if (ackStartSequenceId == this.sendWindowStartSequenceId)
                        {
                            while (true)
                            {
                                this.sendWindowStartSequenceId = Session.NextSequenceId(this.sendWindowStartSequenceId);

                                if (this.sendWindowData.ContainsKey(this.sendWindowStartSequenceId))
                                {
                                    break;
                                }

                                if (this.sendWindowStartSequenceId == this.sendWindowEndSequenceId)
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

                if (Session.CompareSequenceIds(packet.SequenceId, this.receiveWindowStartSeq) >= 0)
                {
                    if (this.receiveWindowData.ContainsKey(packet.SequenceId) == false)
                    {
                        if (this.receiveWindowUsed + packet.Data.Length > this.receiveWindowSize)
                        {
                            throw new RecieveWindowFullException();
                        }

                        this.receiveWindowData.TryAdd(packet.SequenceId, packet.Data);
                        this.receiveWindowUsed += packet.Data.Length;

                        if (packet.SequenceId == this.receiveWindowStartSeq)
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

            await this.SendHandshakePacketAsync(this.Options.MaxRetransmissionTimeout);
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

                    if (this.receiveWindowData.ContainsKey(this.receiveWindowStartSeq))
                    {
                        break;
                    }

                    var timeout = Constants.MaximumWaitTime.ToTimeoutChannel();
                    var cts = new CancellationTokenSource();
                    var channelTasks = new List<Task<Channel<uint?>>>
                    {
                        this.recieveDataUpdateChannel.Shift(cts.Token),
                        timeout.Shift(cts.Token),
                        this.readContext.DoneChannel.Shift(cts.Token)
                    };

                    var channel = await channelTasks.FirstAsync(cts);
                    if (channel == this.readContext.DoneChannel)
                    {
                        throw this.readContext.Error;
                    }
                }

                byte[] buffer = default;
                var currentBytesReceived = 0;

                var data = this.receiveWindowData[this.receiveWindowStartSeq];
                if (this.IsStream == false && maxSize > 0 && maxSize < data.Length)
                {
                    throw new BufferSizeTooSmallException();
                }

                buffer = data.Concat(Enumerable.Empty<byte>()).ToArray();
                currentBytesReceived = data.Length;
                if (maxSize > 0)
                {
                    buffer = new byte[maxSize];

                    var length = maxSize > data.Length ? data.Length : maxSize;

                    Array.Copy(data, buffer, length);

                    currentBytesReceived = length;
                }

                if (currentBytesReceived == data.Length)
                {
                    this.receiveWindowData.TryRemove(this.receiveWindowStartSeq, out _);
                    this.receiveWindowStartSeq = Session.NextSequenceId(this.receiveWindowStartSeq);
                }
                else
                {
                    var subarray = data.Skip(currentBytesReceived).ToArray();
                    if (this.receiveWindowData.ContainsKey(this.receiveWindowStartSeq))
                    {
                        this.receiveWindowData[this.receiveWindowStartSeq] = subarray;
                    }
                    else
                    {
                        this.receiveWindowData.TryAdd(this.receiveWindowStartSeq, subarray);
                    }
                }

                this.receiveWindowUsed -= currentBytesReceived;
                this.TotalBytesRead += (uint)currentBytesReceived;
                this.bytesReadUpdateTime = DateTime.Now;

                if (this.IsStream)
                {
                    while (maxSize < 0 || currentBytesReceived < maxSize)
                    {
                        if (!this.receiveWindowData.ContainsKey(this.receiveWindowStartSeq))
                        {
                            break;
                        }

                        data = this.receiveWindowData[this.receiveWindowStartSeq];

                        int countOfBytesToRead;

                        if (maxSize > 0)
                        {
                            var length = maxSize > data.Length ? data.Length : maxSize;

                            Array.Copy(data, 0, buffer, currentBytesReceived, length);

                            countOfBytesToRead = length;
                        }
                        else
                        {
                            buffer = buffer.Concat(data).ToArray();

                            countOfBytesToRead = data.Length;
                        }

                        if (countOfBytesToRead == data.Length)
                        {
                            this.receiveWindowData.TryRemove(this.receiveWindowStartSeq, out _);
                            this.receiveWindowStartSeq = Session.NextSequenceId(this.receiveWindowStartSeq);
                        }
                        else
                        {
                            this.receiveWindowData.TryAdd(this.receiveWindowStartSeq, data.Skip(countOfBytesToRead).ToArray());
                        }

                        this.receiveWindowUsed -= countOfBytesToRead;
                        this.TotalBytesRead += (uint)countOfBytesToRead;
                        this.bytesReadUpdateTime = DateTime.Now;

                        currentBytesReceived += countOfBytesToRead;
                    }

                }
                return buffer.Take(currentBytesReceived).ToArray();
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

                        this.totalBytesWritten += bytesToSend;
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
                    this.totalBytesWritten += (ulong)data.Length;
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

        public async Task CloseAsync()
        {
            await this.readContext.CancelAsync();
            await this.writeContext.CancelAsync();

            var timeout = Channel.CreateBounded<uint?>(1);

            if (this.Options.Linger > 0)
            {
                Task.Run(async delegate
                {
                    await Task.Delay(this.Options.Linger);
                    await timeout.CompleteAsync();
                });
            }

            if (this.Options.Linger != 0)
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
                            if (this.sendWindowStartSequenceId == this.sendWindowEndSequenceId)
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

        private static int CompareSequenceIds(uint sequenceId1, uint sequenceId2)
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
                var timeout = this.Options.FlushInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    timeout.Shift(cts.Token),
                    this.Context.DoneChannel.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.Context.DoneChannel)
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
                var timeout = this.Options.CheckBytesReadInterval.ToTimeoutChannel();
                var cts = new CancellationTokenSource();
                var channelTasks = new List<Task<Channel<uint?>>>
                {
                    timeout.Shift(cts.Token),
                    this.Context.DoneChannel.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == this.Context.DoneChannel)
                {
                    throw this.Context.Error;
                }

                if (this.TotalBytesRead == 0
                    || this.AckSentTime > this.bytesReadUpdateTime
                    || (DateTime.Now - this.bytesReadUpdateTime).TotalMilliseconds < this.Options.SendBytesReadThreshold)
                {
                    continue;
                }

                try
                {
                    var packet = new Packet { BytesRead = this.TotalBytesRead };
                    var data = ProtoSerializer.Serialize(packet);
                    var tasks = new Task[this.connections.Count];
                    var i = 0;
                    foreach (var connection in this.connections.Values)
                    {
                        tasks[i] = Task.Run(async () => await this.SendDataAsync(connection.LocalClientId, connection.RemoteClientId, data));
                        i++;
                    }

                    await Task.WhenAny(tasks);

                    this.AckSentTime = DateTime.Now;
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
                    context.DoneChannel.Shift(cts.Token)
                };

                var channel = await channelTasks.FirstAsync(cts);
                if (channel == context.DoneChannel)
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

            lock (this.syncLock)
            {
                if (this.sendBuffer == null || this.sendBuffer.Length == 0)
                {
                    return;
                }

                sequenceId = this.sendWindowEndSequenceId;
                var packet = new Packet
                {
                    SequenceId = sequenceId,
                    Data = this.sendBuffer
                };

                var data = ProtoSerializer.Serialize(packet);

                this.sendWindowData.Add(sequenceId, data);

                this.sendWindowEndSequenceId = Session.NextSequenceId(sequenceId);
                this.sendBuffer = new byte[0];
            }

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.sendChannel.Push(sequenceId, cts.Token),
                this.Context.DoneChannel.Shift(cts.Token)
            };

            var channel = await channelTasks.FirstAsync(cts);
            if (channel == this.Context.DoneChannel)
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
                WindowSize = this.receiveWindowSize,
                Mtu = this.recieveMtu
            };

            var data = ProtoSerializer.Serialize(packet);

            var tasks = new List<Task>();

            if (this.connections != null && this.connections.Count > 0)
            {
                foreach (var connection in this.connections.Values)
                {
                    var sendDataTask = this.SendDataAsync(connection.LocalClientId, connection.RemoteClientId, data);

                    tasks.Add(sendDataTask.ToTimeoutTask(timeout, new WriteDeadlineExceededException()));
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

                    var sendDataTask = this.SendDataAsync(this.localClientIds[i], remoteClientId, data);

                    tasks.Add(sendDataTask.ToTimeoutTask(timeout, new WriteDeadlineExceededException()));
                }
            }
           
            await Task.WhenAny(tasks.ToArray());
        }

        private ulong GetSendWindowUsed()
        {
            if (this.totalBytesWritten > this.remoteBytesRead)
            {
                return this.totalBytesWritten - this.remoteBytesRead;
            }

            return 0;
        }

        private void HandleHandshakePacketAsync(Packet packet)
        {
            if (this.isEstablished)
            {
                return;
            }

            this.ValidateHandshakePacket(packet);
            this.InitializeConnections(packet.ClientIds);

            this.remoteClientIds = packet.ClientIds;
            this.sendBuffer = new byte[0];

            this.sendChannel = Channel.CreateUnbounded<uint?>();
            this.ResendChannel = Channel.CreateBounded<uint?>(this.Options.MaxConnectionWindowSize * this.connections.Count);
            this.sendWindowUpdateChannel = Channel.CreateBounded<uint?>(1);
            this.recieveDataUpdateChannel = Channel.CreateBounded<uint?>(1);

            this.sendWindowData = new ConcurrentDictionary<uint, byte[]>();
            this.receiveWindowData = new ConcurrentDictionary<uint, byte[]>();
            this.isEstablished = true;

            var cts = new CancellationTokenSource();
            var channelTasks = new List<Task<Channel<uint?>>>
            {
                this.onAcceptChannel.Push(null, cts.Token),
                Constants.ClosedChannel.Shift(cts.Token)
            };

            Task.Run(() => channelTasks.FirstAsync(cts));
        }

        private void InitializeConnections(IList<string> remoteClientIds)
        {
            var connectionsCount = this.localClientIds.Count();
            if (remoteClientIds.Count < connectionsCount)
            {
                connectionsCount = remoteClientIds.Count;
            }

            IDictionary<string, Connection> connections = new ConcurrentDictionary<string, Connection>();
            for (int i = 0; i < connectionsCount; i++)
            {
                var connection = new Connection(this, this.localClientIds[i], remoteClientIds[i]);
                var connectionKey = Connection.GetKey(connection.LocalClientId, connection.RemoteClientId);

                connections.Add(connectionKey, connection);
            }

            this.connections = connections;
        }

        private void ValidateHandshakePacket(Packet packet)
        {
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
        }

        private async Task SendClosePacketAsync()
        {
            if (this.isEstablished == false)
            {
                throw new SessionNotEstablishedException();
            }

            var packet = new Packet { IsClose = true };
            var data = ProtoSerializer.Serialize(packet);

            var sendDataTasks = new Task[this.connections.Count];
            var i = 0;
            foreach (var connection in this.connections.Values)
            {
                var sendDataTask = this.SendDataAsync(connection.LocalClientId, connection.RemoteClientId, data);

                sendDataTasks[i] = sendDataTask.ToTimeoutTask(
                    connection.RetransmissionTimeout, 
                    new WriteDeadlineExceededException());
            }

            await Task.WhenAny(sendDataTasks);
        }

        private void HandleClosePacket()
        {
            Task.Run(this.readContext.CancelAsync);
            Task.Run(this.writeContext.CancelAsync);
            Task.Run(this.Context.CancelAsync);

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
    }
}
