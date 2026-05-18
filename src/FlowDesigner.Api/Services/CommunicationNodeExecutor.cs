using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public interface ICommunicationNodeExecutor
{
    Task<string> CreateConnectionAsync(string nodeType, object config);
    Task<bool> SendDataAsync(string connectionId, byte[] data);
    Task DisconnectAsync(string connectionId);
    void RegisterDataHandler(string connectionId, Action<string, byte[]> handler);
    Task<PerformanceMetrics?> GetMetricsAsync(string connectionId);
}

public class CommunicationNodeExecutor : ICommunicationNodeExecutor, IDisposable
{
    private readonly EnhancedTcpService _tcpService;
    private readonly EnhancedRtpService _rtpService;
    private readonly EnhancedWebSocketService _webSocketService;
    private readonly CommunicationPerformanceMonitor _performanceMonitor;
    private readonly BackpressureController _backpressureController;
    
    private readonly ConcurrentDictionary<string, CommunicationNodeInfo> _activeNodes = new();
    private readonly ConcurrentDictionary<string, string> _connectionToServiceMap = new();
    
    private bool _disposed;

    public CommunicationNodeExecutor(
        EnhancedTcpService? tcpService = null,
        EnhancedRtpService? rtpService = null,
        EnhancedWebSocketService? webSocketService = null)
    {
        _tcpService = tcpService ?? new EnhancedTcpService();
        _rtpService = rtpService ?? new EnhancedRtpService();
        _webSocketService = webSocketService ?? new EnhancedWebSocketService();
        _performanceMonitor = new CommunicationPerformanceMonitor();
        _backpressureController = new BackpressureController();
    }

    public async Task<string> CreateConnectionAsync(string nodeType, object config)
    {
        var connectionId = Guid.NewGuid().ToString();
        
        var nodeInfo = new CommunicationNodeInfo
        {
            ConnectionId = connectionId,
            NodeType = nodeType,
            CreatedAt = DateTime.UtcNow,
            Status = "Connecting"
        };
        
        _activeNodes[connectionId] = nodeInfo;
        
        try
        {
            switch (nodeType.ToLower())
            {
                case "tcp-client":
                case "tcp-server":
                    var tcpConfig = ParseTcpConfig(config);
                    var tcpId = await _tcpService.ConnectAsync(tcpConfig);
                    _connectionToServiceMap[connectionId] = "tcp";
                    nodeInfo.ExternalId = tcpId;
                    nodeInfo.Status = tcpConfig.IsServer ? "Listening" : "Connected";
                    break;
                    
                case "rtp-sender":
                case "rtp-receiver":
                    var rtpConfig = ParseRtpConfig(config);
                    var rtpId = await _rtpService.StartSessionAsync(rtpConfig);
                    _connectionToServiceMap[connectionId] = "rtp";
                    nodeInfo.ExternalId = rtpId;
                    nodeInfo.Status = "Active";
                    break;
                    
                case "websocket-in":
                case "websocket-out":
                case "websocket-server":
                    var wsConfig = ParseWebSocketConfig(config);
                    var wsId = await _webSocketService.ConnectAsync(wsConfig);
                    _connectionToServiceMap[connectionId] = "websocket";
                    nodeInfo.ExternalId = wsId;
                    nodeInfo.Status = "Connected";
                    break;
                    
                default:
                    throw new NotSupportedException($"不支持的节点类型: {nodeType}");
            }
        }
        catch (Exception ex)
        {
            nodeInfo.Status = $"Error: {ex.Message}";
            throw;
        }
        
        return connectionId;
    }

    public async Task<bool> SendDataAsync(string connectionId, byte[] data)
    {
        if (!_activeNodes.TryGetValue(connectionId, out var nodeInfo))
            return false;
        
        if (!_connectionToServiceMap.TryGetValue(connectionId, out var serviceType))
            return false;
        
        if (!_backpressureController.CanAccept(connectionId))
        {
            return false;
        }
        
        try
        {
            switch (serviceType)
            {
                case "tcp":
                    return await _tcpService.SendAsync(nodeInfo.ExternalId, data);
                    
                case "rtp":
                    return await _rtpService.SendPacketAsync(nodeInfo.ExternalId, data);
                    
                case "websocket":
                    var wsMessage = new WebSocketMessage
                    {
                        Type = "binary",
                        Payload = data,
                        Timestamp = DateTime.UtcNow
                    };
                    return await _webSocketService.SendAsync(nodeInfo.ExternalId, wsMessage);
                    
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_activeNodes.TryGetValue(connectionId, out var nodeInfo))
            return;
        
        if (!_connectionToServiceMap.TryGetValue(connectionId, out var serviceType))
            return;
        
        try
        {
            switch (serviceType)
            {
                case "tcp":
                    await _tcpService.DisconnectAsync(nodeInfo.ExternalId);
                    break;
                    
                case "rtp":
                    await _rtpService.StopSessionAsync(nodeInfo.ExternalId);
                    break;
                    
                case "websocket":
                    await _webSocketService.DisconnectAsync(nodeInfo.ExternalId);
                    break;
            }
        }
        catch
        {
        }
        finally
        {
            _activeNodes.TryRemove(connectionId, out _);
            _connectionToServiceMap.TryRemove(connectionId, out _);
            _backpressureController.RemoveConnection(connectionId);
        }
    }

    public void RegisterDataHandler(string connectionId, Action<string, byte[]> handler)
    {
        if (!_activeNodes.TryGetValue(connectionId, out var nodeInfo))
            return;
        
        if (!_connectionToServiceMap.TryGetValue(connectionId, out var serviceType))
            return;
        
        switch (serviceType)
        {
            case "tcp":
                _tcpService.RegisterDataHandlerAsync(nodeInfo.ExternalId, data => handler(connectionId, data));
                break;
                
            case "rtp":
                _rtpService.RegisterPacketHandlerAsync(nodeInfo.ExternalId, packet => handler(connectionId, packet.Data));
                break;
                
            case "websocket":
                _webSocketService.RegisterMessageHandlerAsync(nodeInfo.ExternalId, message =>
                {
                    if (message.Payload is byte[] data)
                    {
                        handler(connectionId, data);
                    }
                    else if (message.Payload is string text)
                    {
                        var outgoingData = System.Text.Encoding.UTF8.GetBytes(text);
                        handler(connectionId, outgoingData);
                    }
                });
                break;
        }
    }

    public async Task<PerformanceMetrics?> GetMetricsAsync(string connectionId)
    {
        if (!_activeNodes.TryGetValue(connectionId, out var nodeInfo))
            return null;
        
        if (!_connectionToServiceMap.TryGetValue(connectionId, out var serviceType))
            return null;
        
        var metrics = new PerformanceMetrics
        {
            ConnectionId = connectionId,
            ServiceName = serviceType
        };
        
        switch (serviceType)
        {
            case "tcp":
                var tcpMetrics = await _tcpService.GetPerformanceMetricsAsync();
                var tcpInfo = await _tcpService.GetConnectionAsync(nodeInfo.ExternalId);
                if (tcpInfo != null)
                {
                    metrics.BytesSent = tcpInfo.BytesSent;
                    metrics.BytesReceived = tcpInfo.BytesReceived;
                    metrics.MessagesSent = tcpInfo.MessagesSent;
                    metrics.MessagesReceived = tcpInfo.MessagesReceived;
                }
                break;
                
            case "rtp":
                var rtpStats = await _rtpService.GetStatisticsAsync(nodeInfo.ExternalId);
                var rtpSession = await _rtpService.GetSessionAsync(nodeInfo.ExternalId);
                if (rtpStats != null)
                {
                    metrics.MessagesReceived = rtpStats.PacketsReceived;
                }
                if (rtpSession != null)
                {
                    metrics.MessagesSent = rtpSession.PacketsSent;
                }
                break;
                
            case "websocket":
                var wsInfo = await _webSocketService.GetConnectionAsync(nodeInfo.ExternalId);
                if (wsInfo != null)
                {
                    metrics.BytesSent = wsInfo.MessageSentCount * 100;
                    metrics.BytesReceived = wsInfo.MessageReceivedCount * 100;
                    metrics.MessagesSent = wsInfo.MessageSentCount;
                    metrics.MessagesReceived = wsInfo.MessageReceivedCount;
                }
                break;
        }
        
        return metrics;
    }

    private TcpConfig ParseTcpConfig(object config)
    {
        if (config is TcpConfig tcpConfig)
            return tcpConfig;
        
        if (config is System.Text.Json.JsonElement json)
        {
            return new TcpConfig
            {
                Host = json.TryGetProperty("host", out var host) ? host.GetString() ?? "127.0.0.1" : "127.0.0.1",
                Port = json.TryGetProperty("port", out var port) ? port.GetInt32() : 8080,
                IsServer = json.TryGetProperty("isServer", out var server) && server.GetBoolean(),
                AutoReconnect = json.TryGetProperty("autoReconnect", out var auto) ? auto.GetBoolean() : true,
                ReconnectInterval = json.TryGetProperty("reconnectInterval", out var interval) ? interval.GetInt32() : 3000,
                MaxReconnectAttempts = json.TryGetProperty("maxReconnectAttempts", out var attempts) ? attempts.GetInt32() : 10,
                UseDelimiter = json.TryGetProperty("useDelimiter", out var delim) ? delim.GetBoolean() : true,
                Delimiter = json.TryGetProperty("delimiter", out var del) ? del.GetString() ?? "\n" : "\n",
                ReceiveBufferSize = json.TryGetProperty("bufferSize", out var buf) ? buf.GetInt32() : 8192
            };
        }
        
        return new TcpConfig();
    }

    private RtpConfig ParseRtpConfig(object config)
    {
        if (config is RtpConfig rtpConfig)
            return rtpConfig;
        
        if (config is System.Text.Json.JsonElement json)
        {
            return new RtpConfig
            {
                Host = json.TryGetProperty("host", out var host) ? host.GetString() ?? "127.0.0.1" : "127.0.0.1",
                Port = json.TryGetProperty("port", out var port) ? port.GetInt32() : 5004,
                IsSender = json.TryGetProperty("isSender", out var sender) && sender.GetBoolean(),
                Ssrc = json.TryGetProperty("ssrc", out var ssrc) ? ssrc.GetInt32() : 12345,
                PayloadType = json.TryGetProperty("payloadType", out var pt) ? pt.GetInt32() : 0,
                ClockRate = json.TryGetProperty("clockRate", out var cr) ? cr.GetInt32() : 8000,
                Multicast = json.TryGetProperty("multicast", out var mc) && mc.GetBoolean(),
                Ttl = json.TryGetProperty("ttl", out var ttl) ? ttl.GetInt32() : 64
            };
        }
        
        return new RtpConfig();
    }

    private WebSocketConfig ParseWebSocketConfig(object config)
    {
        if (config is WebSocketConfig wsConfig)
            return wsConfig;
        
        if (config is System.Text.Json.JsonElement json)
        {
            return new WebSocketConfig
            {
                Url = json.TryGetProperty("url", out var url) ? url.GetString() ?? "ws://localhost:8080" : "ws://localhost:8080",
                AutoReconnect = json.TryGetProperty("autoReconnect", out var auto) ? auto.GetBoolean() : true,
                ReconnectInterval = json.TryGetProperty("reconnectInterval", out var interval) ? interval.GetInt32() : 3000,
                MaxReconnectAttempts = json.TryGetProperty("maxReconnectAttempts", out var attempts) ? attempts.GetInt32() : 10,
                ConnectionTimeout = json.TryGetProperty("connectionTimeout", out var timeout) ? timeout.GetInt32() : 5000
            };
        }
        
        return new WebSocketConfig();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        foreach (var node in _activeNodes.Keys)
        {
            DisconnectAsync(node).Wait();
        }
        
        _tcpService.Dispose();
        _rtpService.Dispose();
        _webSocketService.Dispose();
    }
}

public class CommunicationNodeInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
