using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class RtpService
{
    private readonly ConcurrentDictionary<string, UdpClient> _udpClients = new();
    private readonly ConcurrentDictionary<string, RtpSessionInfo> _sessionInfos = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionCts = new();
    private readonly ConcurrentDictionary<string, Action<RtpPacket>> _packetHandlers = new();
    private readonly ConcurrentDictionary<string, ushort> _sequenceNumbers = new();
    private readonly ConcurrentDictionary<string, int> _timestamps = new();
    private readonly Random _random = new();

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

    public async Task<string> StartSessionAsync(RtpConfig config, string sessionName = "")
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

        var cts = new CancellationTokenSource();
        _sessionCts[sessionId] = cts;

        try
        {
            var client = new UdpClient();
            if (config.Multicast)
            {
                client.JoinMulticastGroup(IPAddress.Parse(config.Host));
                client.Ttl = (short)config.Ttl;
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
            SessionStatusChanged?.Invoke(sessionId, false);
            Console.WriteLine($"RTP session start error: {ex.Message}");
            throw;
        }

        return sessionId;
    }

    private async Task ReceivePacketsAsync(string sessionId, UdpClient client, RtpConfig config, CancellationToken cancellationToken)
    {
        var endpoint = new IPEndPoint(IPAddress.Any, config.Port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync();
                var packetData = result.Buffer;

                if (packetData.Length >= 12)
                {
                    var packet = ParseRtpPacket(packetData);
                    if (packet != null)
                    {
                        if (_sessionInfos.TryGetValue(sessionId, out var info))
                        {
                            info.PacketsReceived++;
                            info.BytesReceived += packet.Data.Length;
                            info.LastPacketAt = DateTime.UtcNow;
                            info.LastSequenceNumber = packet.SequenceNumber;
                            info.LastTimestamp = packet.Timestamp;
                        }

                        if (_packetHandlers.TryGetValue(sessionId, out var handler))
                        {
                            handler(packet);
                        }
                        PacketReceived?.Invoke(sessionId, packet);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RTP receive error: {ex.Message}");
        }
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

            var payloadLength = data.Length - headerLength;
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

            return true;
        }
        catch (Exception ex)
        {
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
        if (_sessionCts.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _sessionCts.TryRemove(sessionId, out _);
        }

        if (_udpClients.TryGetValue(sessionId, out var client))
        {
            client.Close();
            client.Dispose();
            _udpClients.TryRemove(sessionId, out _);
        }

        if (_sessionInfos.TryGetValue(sessionId, out var info))
        {
            info.IsActive = false;
        }

        _packetHandlers.TryRemove(sessionId, out _);
        _sequenceNumbers.TryRemove(sessionId, out _);
        _timestamps.TryRemove(sessionId, out _);

        SessionStatusChanged?.Invoke(sessionId, false);
    }

    public Task RegisterPacketHandlerAsync(string sessionId, Action<RtpPacket> handler)
    {
        _packetHandlers[sessionId] = handler;
        return Task.CompletedTask;
    }
}
