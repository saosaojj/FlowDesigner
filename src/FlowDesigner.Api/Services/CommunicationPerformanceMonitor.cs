using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace FlowDesigner.Api.Services;

public class PerformanceMetrics
{
    public string ServiceName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public long TotalConnections { get; set; }
    public long ActiveConnections { get; set; }
    public long MessagesSent { get; set; }
    public long MessagesReceived { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public long Errors { get; set; }
    public double AverageLatencyMs { get; set; }
    public double ThroughputPerSecond { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class LatencySnapshot
{
    public long Timestamp { get; set; }
    public double LatencyMs { get; set; }
}

public class PerformanceMetricsSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<PerformanceMetrics> Metrics { get; set; } = new();
    public long TotalConnections { get; set; }
    public long TotalMessages { get; set; }
    public double TotalThroughput { get; set; }
    public double AverageLatency { get; set; }
}

public class CommunicationPerformanceMonitor
{
    private readonly ConcurrentDictionary<string, ServiceMetrics> _serviceMetrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LatencySnapshot>> _latencyHistory = new();
    private readonly Channel<PerformanceMetricsSnapshot> _snapshotsChannel;
    private readonly Timer _aggregationTimer;
    private readonly int _maxLatencyHistorySize = 1000;
    
    private long _totalMessagesSent;
    private long _totalMessagesReceived;
    private long _totalBytesSent;
    private long _totalBytesReceived;
    private long _totalErrors;
    
    public CommunicationPerformanceMonitor()
    {
        _snapshotsChannel = Channel.CreateBounded<PerformanceMetricsSnapshot>(
            new BoundedChannelOptions(60) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        
        _aggregationTimer = new Timer(
            AggregateMetrics,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1)
        );
    }

    public void RecordConnectionOpen(string serviceName, string connectionId)
    {
        var metrics = _serviceMetrics.GetOrAdd(serviceName, _ => new ServiceMetrics());
        Interlocked.Increment(ref metrics.TotalConnections);
        Interlocked.Increment(ref metrics.ActiveConnections);
    }

    public void RecordConnectionClose(string serviceName, string connectionId)
    {
        if (_serviceMetrics.TryGetValue(serviceName, out var metrics))
        {
            Interlocked.Decrement(ref metrics.ActiveConnections);
        }
    }

    public void RecordMessageSent(string serviceName, long bytes, double latencyMs)
    {
        if (_serviceMetrics.TryGetValue(serviceName, out var metrics))
        {
            Interlocked.Increment(ref metrics.MessagesSent);
            Interlocked.Add(ref metrics.BytesSent, bytes);
            Interlocked.Increment(ref _totalMessagesSent);
            Interlocked.Add(ref _totalBytesSent, bytes);
            
            RecordLatency(serviceName, latencyMs);
        }
    }

    public void RecordMessageReceived(string serviceName, long bytes)
    {
        if (_serviceMetrics.TryGetValue(serviceName, out var metrics))
        {
            Interlocked.Increment(ref metrics.MessagesReceived);
            Interlocked.Add(ref metrics.BytesReceived, bytes);
            Interlocked.Increment(ref _totalMessagesReceived);
            Interlocked.Add(ref _totalBytesReceived, bytes);
        }
    }

    public void RecordError(string serviceName)
    {
        if (_serviceMetrics.TryGetValue(serviceName, out var metrics))
        {
            Interlocked.Increment(ref metrics.Errors);
            Interlocked.Increment(ref _totalErrors);
        }
    }

    private void RecordLatency(string serviceName, double latencyMs)
    {
        var queue = _latencyHistory.GetOrAdd(serviceName, _ => new ConcurrentQueue<LatencySnapshot>());
        
        queue.Enqueue(new LatencySnapshot
        {
            Timestamp = Stopwatch.GetTimestamp(),
            LatencyMs = latencyMs
        });
        
        while (queue.Count > _maxLatencyHistorySize)
        {
            queue.TryDequeue(out _);
        }
        
        if (_serviceMetrics.TryGetValue(serviceName, out var metrics))
        {
            var latencies = queue.Select(l => l.LatencyMs).ToList();
            if (latencies.Any())
            {
                var avg = latencies.Average();
                Interlocked.Exchange(ref metrics.AverageLatencyMs, avg);
                
                var p50 = CalculatePercentile(latencies, 0.5);
                var p95 = CalculatePercentile(latencies, 0.95);
                var p99 = CalculatePercentile(latencies, 0.99);
                
                Interlocked.Exchange(ref metrics.P50Latency, p50);
                Interlocked.Exchange(ref metrics.P95Latency, p95);
                Interlocked.Exchange(ref metrics.P99Latency, p99);
            }
        }
    }

    private double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (!sortedValues.Any()) return 0;
        
        var sorted = sortedValues.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        return sorted[index];
    }

    private void AggregateMetrics(object? state)
    {
        var snapshot = new PerformanceMetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Metrics = _serviceMetrics.Select(kvp => new PerformanceMetrics
            {
                ServiceName = kvp.Value.ServiceName,
                TotalConnections = kvp.Value.TotalConnections,
                ActiveConnections = kvp.Value.ActiveConnections,
                MessagesSent = kvp.Value.MessagesSent,
                MessagesReceived = kvp.Value.MessagesReceived,
                BytesSent = kvp.Value.BytesSent,
                BytesReceived = kvp.Value.BytesReceived,
                Errors = kvp.Value.Errors,
                AverageLatencyMs = kvp.Value.AverageLatencyMs,
                LastUpdated = DateTime.UtcNow
            }).ToList(),
            TotalConnections = _serviceMetrics.Values.Sum(m => m.ActiveConnections),
            TotalMessages = _totalMessagesSent + _totalMessagesReceived,
            TotalThroughput = CalculateThroughput(),
            AverageLatency = _serviceMetrics.Values.Any() 
                ? _serviceMetrics.Values.Average(m => m.AverageLatencyMs) 
                : 0
        };

        _snapshotsChannel.Writer.TryWrite(snapshot);
    }

    private double CalculateThroughput()
    {
        return _serviceMetrics.Values.Sum(m => m.MessagesSent + m.MessagesReceived);
    }

    public async Task<PerformanceMetricsSnapshot> GetCurrentSnapshotAsync()
    {
        if (_snapshotsChannel.Reader.TryRead(out var snapshot))
        {
            return snapshot;
        }
        
        return await Task.FromResult(new PerformanceMetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Metrics = _serviceMetrics.Select(kvp => new PerformanceMetrics
            {
                ServiceName = kvp.Value.ServiceName,
                TotalConnections = kvp.Value.TotalConnections,
                ActiveConnections = kvp.Value.ActiveConnections,
                MessagesSent = kvp.Value.MessagesSent,
                MessagesReceived = kvp.Value.MessagesReceived,
                BytesSent = kvp.Value.BytesSent,
                BytesReceived = kvp.Value.BytesReceived,
                Errors = kvp.Value.Errors,
                AverageLatencyMs = kvp.Value.AverageLatencyMs
            }).ToList()
        });
    }

    public Task<List<PerformanceMetrics>> GetMetricsAsync()
    {
        return Task.FromResult(_serviceMetrics.Select(kvp => new PerformanceMetrics
        {
            ServiceName = kvp.Value.ServiceName,
            TotalConnections = kvp.Value.TotalConnections,
            ActiveConnections = kvp.Value.ActiveConnections,
            MessagesSent = kvp.Value.MessagesSent,
            MessagesReceived = kvp.Value.MessagesReceived,
            BytesSent = kvp.Value.BytesSent,
            BytesReceived = kvp.Value.BytesReceived,
            Errors = kvp.Value.Errors,
            AverageLatencyMs = kvp.Value.AverageLatencyMs,
            ThroughputPerSecond = CalculateThroughput(),
            LastUpdated = DateTime.UtcNow
        }).ToList());
    }

    private class ServiceMetrics
    {
        public string ServiceName { get; set; } = string.Empty;
        public long TotalConnections;
        public long ActiveConnections;
        public long MessagesSent;
        public long MessagesReceived;
        public long BytesSent;
        public long BytesReceived;
        public long Errors;
        public double AverageLatencyMs;
        public double P50Latency;
        public double P95Latency;
        public double P99Latency;
    }
}
