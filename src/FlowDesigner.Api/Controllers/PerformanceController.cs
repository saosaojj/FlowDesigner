using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FlowDesigner.Shared.Models;
using FlowDesigner.Api.Services;

namespace FlowDesigner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformanceController : ControllerBase
{
    private readonly CommunicationPerformanceMonitor _communicationMonitor;
    private readonly BackpressureController _backpressureController;
    private readonly EnhancedTcpService _tcpService;
    private readonly EnhancedRtpService _rtpService;
    private readonly EnhancedWebSocketService _webSocketService;
    private readonly CommunicationNodeExecutor _communicationExecutor;

    public PerformanceController(
        CommunicationPerformanceMonitor communicationMonitor,
        BackpressureController backpressureController,
        EnhancedTcpService tcpService,
        EnhancedRtpService rtpService,
        EnhancedWebSocketService webSocketService,
        CommunicationNodeExecutor communicationExecutor)
    {
        _communicationMonitor = communicationMonitor;
        _backpressureController = backpressureController;
        _tcpService = tcpService;
        _rtpService = rtpService;
        _webSocketService = webSocketService;
        _communicationExecutor = communicationExecutor;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<PerformanceOverview>> GetOverview()
    {
        var tcpConnections = await _tcpService.GetAllConnectionsAsync();
        var rtpSessions = await _rtpService.GetAllSessionsAsync();
        var wsConnections = await _webSocketService.GetAllConnectionsAsync();
        var commMetrics = await _communicationMonitor.GetCurrentSnapshotAsync();

        return new PerformanceOverview
        {
            Timestamp = System.DateTime.UtcNow,
            TcpConnections = new ConnectionStats
            {
                Total = tcpConnections.Count,
                Active = tcpConnections.FindAll(c => c.Status == TcpConnectionStatus.Connected || c.Status == TcpConnectionStatus.Listening).Count,
                TotalBytesSent = tcpConnections.Count > 0 ? tcpConnections[0].BytesSent : 0,
                TotalBytesReceived = tcpConnections.Count > 0 ? tcpConnections[0].BytesReceived : 0
            },
            RtpSessions = new ConnectionStats
            {
                Total = rtpSessions.Count,
                Active = rtpSessions.FindAll(s => s.IsActive).Count
            },
            WebSocketConnections = new ConnectionStats
            {
                Total = wsConnections.Count,
                Active = wsConnections.Count
            },
            BackpressureStates = _backpressureController.GetAllStates().Count,
            SystemMetrics = commMetrics
        };
    }

    [HttpGet("tcp")]
    public async Task<ActionResult<TcpPerformanceData>> GetTcpPerformance()
    {
        var connections = await _tcpService.GetAllConnectionsAsync();
        var metrics = await _tcpService.GetPerformanceMetricsAsync();
        var backpressureStates = new List<BackpressureState>();

        foreach (var conn in connections)
        {
            var bp = await _tcpService.GetBackpressureStateAsync(conn.Id);
            if (bp != null)
            {
                backpressureStates.Add(bp);
            }
        }

        return new TcpPerformanceData
        {
            Connections = connections,
            BackpressureStates = backpressureStates,
            SystemMetrics = metrics
        };
    }

    [HttpGet("rtp")]
    public async Task<ActionResult<RtpPerformanceData>> GetRtpPerformance()
    {
        var sessions = await _rtpService.GetAllSessionsAsync();
        var metrics = await _rtpService.GetPerformanceMetricsAsync();
        var statistics = new List<RtpSessionStatistics>();

        foreach (var session in sessions)
        {
            var stats = await _rtpService.GetStatisticsAsync(session.Id);
            statistics.Add(new RtpSessionStatistics
            {
                SessionId = session.Id,
                SessionName = session.Name,
                IsActive = session.IsActive,
                PacketsReceived = session.PacketsReceived,
                PacketsSent = session.PacketsSent,
                BytesReceived = session.BytesReceived,
                BytesSent = session.BytesSent,
                JitterBufferStats = stats != null ? new JitterBufferStats
                {
                    PacketsReceived = stats.PacketsReceived,
                    PacketsLost = stats.PacketsLost,
                    PacketsOutOfOrder = stats.PacketsOutOfOrder,
                    DuplicatePackets = stats.DuplicatePackets,
                    AverageJitterMs = stats.AverageJitterMs,
                    MaxJitterMs = stats.MaxJitterMs,
                    MinJitterMs = stats.MinJitterMs,
                    PacketLossPercent = stats.PacketLossPercent,
                    BufferOccupancy = stats.BufferOccupancy
                } : null
            });
        }

        return new RtpPerformanceData
        {
            Sessions = sessions,
            Statistics = statistics,
            SystemMetrics = metrics
        };
    }

    [HttpGet("websocket")]
    public async Task<ActionResult<WebSocketPerformanceData>> GetWebSocketPerformance()
    {
        var connections = await _webSocketService.GetAllConnectionsAsync();
        var metrics = await _webSocketService.GetPerformanceMetricsAsync();
        var backpressureStates = new List<BackpressureState>();

        foreach (var conn in connections)
        {
            var bp = await _webSocketService.GetBackpressureStateAsync(conn.Id);
            if (bp != null)
            {
                backpressureStates.Add(bp);
            }
        }

        return new WebSocketPerformanceData
        {
            Connections = connections,
            BackpressureStates = backpressureStates,
            SystemMetrics = metrics
        };
    }

    [HttpGet("backpressure")]
    public ActionResult<Dictionary<string, BackpressureState>> GetBackpressureStates()
    {
        return _backpressureController.GetAllStates();
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<PerformanceMetricsSnapshot>> GetMetrics()
    {
        return await _communicationMonitor.GetCurrentSnapshotAsync();
    }
}

public class PerformanceOverview
{
    public System.DateTime Timestamp { get; set; }
    public ConnectionStats TcpConnections { get; set; } = new();
    public ConnectionStats RtpSessions { get; set; } = new();
    public ConnectionStats WebSocketConnections { get; set; } = new();
    public int BackpressureStates { get; set; }
    public PerformanceMetricsSnapshot? SystemMetrics { get; set; }
}

public class ConnectionStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public long TotalBytesSent { get; set; }
    public long TotalBytesReceived { get; set; }
}

public class TcpPerformanceData
{
    public List<TcpConnectionInfo> Connections { get; set; } = new();
    public List<BackpressureState> BackpressureStates { get; set; } = new();
    public PerformanceMetricsSnapshot? SystemMetrics { get; set; }
}

public class RtpPerformanceData
{
    public List<RtpSessionInfo> Sessions { get; set; } = new();
    public List<RtpSessionStatistics> Statistics { get; set; } = new();
    public PerformanceMetricsSnapshot? SystemMetrics { get; set; }
}

public class RtpSessionStatistics
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public long PacketsReceived { get; set; }
    public long PacketsSent { get; set; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public JitterBufferStats? JitterBufferStats { get; set; }
}

public class JitterBufferStats
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
}

public class WebSocketPerformanceData
{
    public List<WebSocketConnectionInfo> Connections { get; set; } = new();
    public List<BackpressureState> BackpressureStates { get; set; } = new();
    public PerformanceMetricsSnapshot? SystemMetrics { get; set; }
}
