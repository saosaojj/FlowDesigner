using System;
using System.Collections.Generic;

namespace FlowDesigner.Shared.Models;

// WebSocket 连接状态
public enum WebSocketConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

// WebSocket 消息
public class WebSocketMessage
{
    public string Type { get; set; } = "text"; // text, binary, json
    public object? Payload { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string>? Headers { get; set; }
}

// WebSocket 连接配置
public class WebSocketConfig
{
    public string Url { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectInterval { get; set; } = 3000; // ms
    public int MaxReconnectAttempts { get; set; } = 10;
    public Dictionary<string, string>? Headers { get; set; }
    public int ConnectionTimeout { get; set; } = 5000; // ms
}

// WebSocket 连接信息
public class WebSocketConnectionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public WebSocketConnectionStatus Status { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public long MessageSentCount { get; set; }
    public long MessageReceivedCount { get; set; }
    public long ErrorCount { get; set; }
}
