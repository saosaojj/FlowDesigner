using System;
using System.Collections.Generic;

namespace FlowDesigner.Shared.Models;

// TCP 连接状态
public enum TcpConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Listening,
    Error
}

// TCP 连接配置
public class TcpConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool IsServer { get; set; } = false;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectInterval { get; set; } = 3000;
    public int MaxReconnectAttempts { get; set; } = 10;
    public int ConnectionTimeout { get; set; } = 5000;
    public int ReceiveBufferSize { get; set; } = 8192;
    public string Delimiter { get; set; } = "\n";
    public bool UseDelimiter { get; set; } = true;
}

// TCP 连接信息
public class TcpConnectionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsServer { get; set; }
    public TcpConnectionStatus Status { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastDataAt { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public long MessagesSent { get; set; }
    public long MessagesReceived { get; set; }
    public long ErrorCount { get; set; }
    public List<string>? ClientIds { get; set; }
}

// RTP 配置
public class RtpConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5004;
    public bool IsSender { get; set; } = false;
    public int Ssrc { get; set; } = (int)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);
    public int PayloadType { get; set; } = 0; // PCMU
    public int ClockRate { get; set; } = 8000;
    public bool Multicast { get; set; } = false;
    public int Ttl { get; set; } = 64;
}

// RTP 包信息
public class RtpPacket
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Timestamp { get; set; }
    public ushort SequenceNumber { get; set; }
    public int Ssrc { get; set; }
    public int PayloadType { get; set; }
    public bool Marker { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

// RTP 会话信息
public class RtpSessionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsSender { get; set; }
    public int Ssrc { get; set; }
    public bool IsActive { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastPacketAt { get; set; }
    public long PacketsSent { get; set; }
    public long PacketsReceived { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public ushort LastSequenceNumber { get; set; }
    public int LastTimestamp { get; set; }
}
