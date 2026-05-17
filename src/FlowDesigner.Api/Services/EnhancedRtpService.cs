using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class JitterBuffer
{
    private readonly SortedDictionary<ushort, RtpPacket> _buffer = new();
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly int _maxPackets;
    private DateTime _lastPlayoutTime = DateTime.MinValue;
    private ushort _lastSequenceNumber;
    private bool _initialized;
    
    private readonly object _lock = new();

    public JitterBuffer(int minDelayMs = 20, int maxDelayMs = 100, int maxPackets = 100)
    {
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
        _maxPackets = maxPackets;
    }

    public void AddPacket(RtpPacket packet)
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                _lastSequenceNumber = packet.SequenceNumber;
                _initialized = true;
            }
            
            if (IsNewerSequence(packet.SequenceNumber, _lastSequenceNumber))
            {
                _lastSequenceNumber = packet.SequenceNumber;
            }
            
            _buffer[packet.SequenceNumber] = packet;
            
            while (_buffer.Count > _maxPackets)
            {
                var oldest = _buffer.Keys.First();
                _buffer.Remove(oldest);
            }
        }
    }

    public RtpPacket? GetNextPacket()
    {
        lock (_lock)
        {
            if (_buffer.Count == 0)
                return null;

            var now = DateTime.UtcNow;
            if (_lastPlayoutTime == DateTime.MinValue)
            {
                _lastPlayoutTime = now.AddMilliseconds(_minDelayMs);
                var first = _buffer.Values.First();
                return first;
            }

            var elapsed = (now - _lastPlayoutTime).TotalMilliseconds;
            if (elapsed < _minDelayMs)
                return null;

            if (_buffer.Count >= _maxPackets / 2 || elapsed >= _maxDelayMs)
            {
                _lastPlayoutTime = now;
                var packet = _buffer.Values.First();
                _buffer.Remove(packet.SequenceNumber);
                return packet;
            }

            return null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _initialized = false;
            _lastSequenceNumber = 0;
            _lastPlayoutTime = DateTime.MinValue;
        }
    }

    public int Count => _buffer.Count;

    private bool IsNewerSequence(ushort seq1, ushort seq2)
    {
        return ((seq1 - seq2) & 0xFFFF) < 0x8000;
    }
}

public class RtpStatistics
{
    public long PacketsReceived { get; set; }
    public long PacketsLost { get; set; }
    public long PacketsOutOfOrder { get; set; }
    public long DuplicatePackets { get; set; }
    public double AverageJitterMs { get; set; }
    public double MaxJitterMs { get; set; }
    public double MinJitterMs { get; set; }
    public double PacketLossPercent { get; set; }
    public int BufferOccupancy { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class EnhancedRtpService : IDisposable
{
    private readonly ConcurrentDictionary<string, UdpClient> _udpClients = new();
    private readonly ConcurrentDictionary<string, RtpSessionInfo> _sessionInfos = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionCts = new();
    private readonly ConcurrentDictionary<string, Action<RtpPacket>> _packetHandlers = new();
    private readonly ConcurrentDictionary<string, ushort> _sequenceNumbers = new();
    private readonly ConcurrentDictionary<string, int> _timestamps = new();
    private readonly ConcurrentDictionary<string, JitterBuffer> _jitterBuffers = new();
    private readonly ConcurrentDictionary<string, RtpStatistics> _statistics = new();
    private readonly ConcurrentDictionary<string, Queue<int>> _interArrivalTimes = new();
    
    private readonly ConnectionPool<UdpClient> _connectionPool;
    private readonly BackpressureController _backpressureController;
    private readonly CommunicationPerformanceMonitor _performanceMonitor;
    
    private readonly Random _random = new();
    private bool _disposed;

    public EnhancedRtpService(
        ConnectionPool<UdpClient>? connectionPool = null,
        BackpressureController? backpressureController = null,
        CommunicationPerformanceMonitor? performanceMonitor = null)
    {
        _connectionPool = connectionPool ?? new ConnectionPool<UdpClient>(
            _ => new UdpClient(),
            client => { try { client.Close(); client.Dispose(); } catch { } },
            20
        );
        _backpressureController = backpressureController ?? new BackpressureController();
        _performanceMonitor = performanceMonitor ?? new CommunicationPerformanceMonitor();
    }

    public event Action<string, RtpPacket>? PacketReceived;
    public event Action<string, bool>? SessionStatusChanged;

    public Task<List<RtpSessionInfo>> GetAllSessionsAsync()
    {
        return Task.FromResult(_sessionInfos.Values.ToList());
    }

    public Task<RtpSessionInfo?> GetSessionAsync(string sessionId)
    {
        _sessionInfos.TryGetValue(sessionId, out var info);
        return Task.FromResult(info);
    }

    public Task<RtpStatistics?> GetStatisticsAsync(string sessionId)
    {
        _statistics.TryGetValue(sessionId, out var stats);
        return Task.FromResult(stats);
    }

    public async Task<string> StartSessionAsync(RtpConfig config, string sessionName = "", bool enableJitterBuffer = true)
    {
        var sessionId = Guid.NewGuid().ToString();
        var name = string.IsNullOrEmpty(sessionName) ? $"RTP-{sessionId}" : sessionName;

        var sessionInfo = new RtpSessionInfo
        {
            Id = sessionId,
            Name = name,
            Host = config.Host,
            Port = config.Port,
            IsSender = config.IsSender,
            Ssrc = config.Ssrc,
            IsActive = true,
            StartedAt = DateTime.UtcNow
        };

        _sessionInfos[sessionId] = sessionInfo;
        _sequenceNumbers[sessionId] = (ushort)_random.Next(0, 65535);
        _timestamps[sessionId] = 0;
        
        _statistics[sessionId] = new RtpStatistics();
        _interArrivalTimes[sessionId] = new Queue<int>();
        
        if (enableJitterBuffer && !config.IsSender)
        {
            _jitterBuffers[sessionId] = new JitterBuffer(minDelayMs: 30, maxDelayMs: 100);
        }

        var cts = new CancellationTokenSource();
        _sessionCts[sessionId] = cts;
        
        _performanceMonitor.RecordConnectionOpen("RTP", sessionId);

        try
        {
            var client = new UdpClient();
            if (config.Multicast)
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.JoinMulticastGroup(IPAddress.Parse(config.Host));
                client.Client.Ttl = (short)config.Ttl;
            }

            if (!config.IsSender)
            {
                client.Client.Bind(new IPEndPoint(IPAddress.Any, config.Port));
            }

            _udpClients[sessionId] = client;
            SessionStatusChanged?.Invoke(sessionId, true);

            if (!config.IsSender)
            {
                _ = ReceivePacketsAsync(sessionId, client, config, cts.Token);
            }
        }
        catch (Exception ex)
        {
            if (_sessionInfos.TryGetValue(sessionId, out var info))
            {
                info.IsActive = false;
            }
            _performanceMonitor.RecordError("RTP");
            SessionStatusChanged?.Invoke(sessionId, false);
            Console.WriteLine($"RTP session start error: {ex.Message}");
            throw;
        }

        return sessionId;
    }

    private async Task ReceivePacketsAsync(string sessionId, UdpClient client, RtpConfig config, CancellationToken cancellationToken)
    {
        var endpoint = new IPEndPoint(IPAddress.Any, config.Port);
        var lastPacketTime = DateTime.UtcNow;
        var lastTimestamp = 0;
        var statistics = _statistics.GetValueOrDefault(sessionId) ?? new RtpStatistics();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(cancellationToken);
                var packetData = result.Buffer;
                var receiveTime = DateTime.UtcNow;

                if (packetData.Length >= 12)
                {
                    var packet = ParseRtpPacket(packetData);
                    if (packet != null)
                    {
                        statistics.PacketsReceived++;
                        _performanceMonitor.RecordMessageReceived("RTP", packetData.Length);
                        
                        if (_jitterBuffers.TryGetValue(sessionId, out var jitterBuffer))
                        {
                            jitterBuffer.AddPacket(packet);
                            
                            while (jitterBuffer.Count > 0)
                            {
                                var bufferedPacket = jitterBuffer.GetNextPacket();
                                if (bufferedPacket != null)
                                {
                                    ProcessPacket(sessionId, bufferedPacket, ref lastTimestamp, ref lastPacketTime, statistics);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ProcessPacket(sessionId, packet, ref lastTimestamp, ref lastPacketTime, statistics);
                        }
                        
                        _statistics[sessionId] = statistics;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("RTP");
            Console.WriteLine($"RTP receive error: {ex.Message}");
        }
    }

    private void ProcessPacket(string sessionId, RtpPacket packet, ref int lastTimestamp, ref DateTime lastPacketTime, RtpStatistics statistics)
    {
        if (_sessionInfos.TryGetValue(sessionId, out var info))
        {
            info.PacketsReceived++;
            info.BytesReceived += packet.Data.Length;
            info.LastPacketAt = DateTime.UtcNow;
            info.LastSequenceNumber = packet.SequenceNumber;
            info.LastTimestamp = packet.Timestamp;
        }

        var now = DateTime.UtcNow;
        var interArrivalTime = (int)(now - lastPacketTime).TotalMilliseconds;
        
        if (_interArrivalTimes.TryGetValue(sessionId, out var queue))
        {
            queue.Enqueue(interArrivalTime);
            while (queue.Count > 100)
            {
                queue.Dequeue();
            }
            
            if (queue.Count > 2)
            {
                var values = queue.ToArray();
                var mean = values.Average();
                var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
                statistics.AverageJitterMs = Math.Sqrt(variance);
                statistics.MaxJitterMs = Math.Max(statistics.MaxJitterMs, statistics.AverageJitterMs);
                if (statistics.MinJitterMs == 0)
                    statistics.MinJitterMs = statistics.AverageJitterMs;
                else
                    statistics.MinJitterMs = Math.Min(statistics.MinJitterMs, statistics.AverageJitterMs);
            }
        }
        
        lastTimestamp = packet.Timestamp;
        lastPacketTime = now;

        if (_jitterBuffers.TryGetValue(sessionId, out var jitterBuffer))
        {
            statistics.BufferOccupancy = jitterBuffer.Count;
        }

        if (_packetHandlers.TryGetValue(sessionId, out var handler))
        {
            handler(packet);
        }
        
        PacketReceived?.Invoke(sessionId, packet);
    }

    private RtpPacket? ParseRtpPacket(byte[] data)
    {
        try
        {
            if (data.Length < 12) return null;

            var version = (data[0] >> 6) & 0x03;
            if (version != 2) return null;

            var padding = (data[0] >> 5) & 0x01;
            var extension = (data[0] >> 4) & 0x01;
            var csrcCount = data[0] & 0x0F;

            var marker = (data[1] >> 7) & 0x01;
            var payloadType = data[1] & 0x7F;

            var sequenceNumber = (ushort)((data[2] << 8) | data[3]);
            var timestamp = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
            var ssrc = (data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11];

            var headerLength = 12 + (csrcCount * 4);

            if (extension != 0 && data.Length >= headerLength + 4)
            {
                var extensionLength = ((data[headerLength + 2] << 8) | data[headerLength + 3]) * 4;
                headerLength += 4 + extensionLength;
            }

            if (padding != 0 && data.Length > headerLength)
            {
                var paddingLength = data[data.Length - 1];
                headerLength += paddingLength;
            }

            var payloadLength = data.Length - headerLength;
            if (payloadLength <= 0) return null;

            var payload = new byte[payloadLength];
            Array.Copy(data, headerLength, payload, 0, payloadLength);

            return new RtpPacket
            {
                Data = payload,
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Ssrc = ssrc,
                PayloadType = payloadType,
                Marker = marker != 0
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SendPacketAsync(string sessionId, byte[] data, bool marker = false)
    {
        if (!_udpClients.TryGetValue(sessionId, out var client) || !_sessionInfos.TryGetValue(sessionId, out var info))
            return false;

        try
        {
            _sequenceNumbers.TryGetValue(sessionId, out var seqNum);
            _timestamps.TryGetValue(sessionId, out var timestamp);

            var packet = BuildRtpPacket(data, info.Ssrc, info.PayloadType, seqNum, timestamp, marker);

            await client.SendAsync(packet, packet.Length, info.Host, info.Port);

            _sequenceNumbers[sessionId] = (ushort)(seqNum + 1);
            _timestamps[sessionId] = timestamp + (data.Length * 8 / (info.ClockRate / 1000));

            if (_sessionInfos.TryGetValue(sessionId, out var sessionInfo))
            {
                sessionInfo.PacketsSent++;
                sessionInfo.BytesSent += data.Length;
                sessionInfo.LastPacketAt = DateTime.UtcNow;
                sessionInfo.LastSequenceNumber = seqNum;
                sessionInfo.LastTimestamp = timestamp;
            }
            
            _performanceMonitor.RecordMessageSent("RTP", packet.Length, 0);

            return true;
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("RTP");
            Console.WriteLine($"RTP send error: {ex.Message}");
            return false;
        }
    }

    private byte[] BuildRtpPacket(byte[] payload, int ssrc, int payloadType, ushort sequenceNumber, int timestamp, bool marker)
    {
        var header = new byte[12];

        header[0] = 0x80;
        header[1] = (byte)((marker ? 0x80 : 0x00) | (payloadType & 0x7F));
        header[2] = (byte)((sequenceNumber >> 8) & 0xFF);
        header[3] = (byte)(sequenceNumber & 0xFF);
        header[4] = (byte)((timestamp >> 24) & 0xFF);
        header[5] = (byte)((timestamp >> 16) & 0xFF);
        header[6] = (byte)((timestamp >> 8) & 0xFF);
        header[7] = (byte)(timestamp & 0xFF);
        header[8] = (byte)((ssrc >> 24) & 0xFF);
        header[9] = (byte)((ssrc >> 16) & 0xFF);
        header[10] = (byte)((ssrc >> 8) & 0xFF);
        header[11] = (byte)(ssrc & 0xFF);

        var packet = new byte[12 + payload.Length];
        Array.Copy(header, packet, 12);
        Array.Copy(payload, 0, packet, 12, payload.Length);

        return packet;
    }

    public async Task StopSessionAsync(string sessionId)
    {
        _performanceMonitor.RecordConnectionClose("RTP", sessionId);
        
        if (_sessionCts.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _sessionCts.TryRemove(sessionId, out _);
        }

        if (_udpClients.TryGetValue(sessionId, out var client))
        {
            try
            {
                client.Close();
            }
            catch { }
            finally
            {
                client.Dispose();
            }
            _udpClients.TryRemove(sessionId, out _);
        }

        if (_sessionInfos.TryGetValue(sessionId, out var info))
        {
            info.IsActive = false;
        }

        _packetHandlers.TryRemove(sessionId, out _);
        _sequenceNumbers.TryRemove(sessionId, out _);
        _timestamps.TryRemove(sessionId, out _);
        _jitterBuffers.TryRemove(sessionId, out _);
        _statistics.TryRemove(sessionId, out _);
        _interArrivalTimes.TryRemove(sessionId, out _);
        _backpressureController.RemoveConnection(sessionId);

        SessionStatusChanged?.Invoke(sessionId, false);
    }

    public Task RegisterPacketHandlerAsync(string sessionId, Action<RtpPacket> handler)
    {
        _packetHandlers[sessionId] = handler;
        return Task.CompletedTask;
    }

    public Task<PerformanceMetricsSnapshot> GetPerformanceMetricsAsync()
    {
        return _performanceMonitor.GetCurrentSnapshotAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var cts in _sessionCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _sessionCts.Clear();

        foreach (var client in _udpClients.Values)
        {
            try
            {
                client.Close();
                client.Dispose();
            }
            catch { }
        }
        _udpClients.Clear();

        _packetHandlers.Clear();
        _sequenceNumbers.Clear();
        _timestamps.Clear();
        _jitterBuffers.Clear();
        _statistics.Clear();
        _interArrivalTimes.Clear();
    }
}
