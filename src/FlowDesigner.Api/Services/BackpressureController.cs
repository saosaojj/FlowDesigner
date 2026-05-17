using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FlowDesigner.Api.Services;

public class BackpressureConfig
{
    public int MaxQueueSize { get; set; } = 10000;
    public int HighWaterMark { get; set; } = 8000;
    public int LowWaterMark { get; set; } = 2000;
    public int RetryDelayMs { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
    public bool EnableDropping { get; set; } = false;
}

public enum BackpressureStatus
{
    Normal,
    High,
    Critical,
    Blocked,
    Dropping
}

public class BackpressureState
{
    public string ConnectionId { get; set; } = string.Empty;
    public BackpressureStatus Status { get; set; } = BackpressureStatus.Normal;
    public int QueueSize { get; set; }
    public double UtilizationPercent => MaxQueueSize > 0 ? (QueueSize * 100.0 / MaxQueueSize) : 0;
    public int MaxQueueSize { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class BackpressureStatistics
{
    public int TotalConnections { get; set; }
    public int NormalCount { get; set; }
    public int HighCount { get; set; }
    public int CriticalCount { get; set; }
    public int BlockedCount { get; set; }
    public int DroppingCount { get; set; }
    public double AverageUtilization { get; set; }
    public double MaxUtilization { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class BackpressureController
{
    private readonly ConcurrentDictionary<string, BackpressureState> _connectionStates = new();
    private readonly ConcurrentDictionary<string, Channel<object>> _queues = new();
    private readonly BackpressureConfig _defaultConfig;
    private readonly Timer _cleanupTimer;

    public BackpressureController(BackpressureConfig? config = null)
    {
        _defaultConfig = config ?? new BackpressureConfig();
        
        _cleanupTimer = new Timer(
            CleanupInactiveQueues,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5)
        );
    }

    public Channel<object> GetOrCreateQueue(string connectionId, BackpressureConfig? config = null)
    {
        var cfg = config ?? _defaultConfig;
        
        return _queues.GetOrAdd(connectionId, _ => 
        {
            var state = new BackpressureState
            {
                ConnectionId = connectionId,
                MaxQueueSize = cfg.MaxQueueSize,
                Status = BackpressureStatus.Normal
            };
            _connectionStates[connectionId] = state;
            
            return Channel.CreateBounded<object>(new BoundedChannelOptions(cfg.MaxQueueSize)
            {
                FullMode = cfg.EnableDropping 
                    ? BoundedChannelFullMode.DropOldest 
                    : BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        });
    }

    public bool CanAccept(string connectionId)
    {
        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            return state.Status == BackpressureStatus.Normal || 
                   state.Status == BackpressureStatus.High;
        }
        return true;
    }

    public BackpressureStatus GetStatus(string connectionId)
    {
        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            return state.Status;
        }
        return BackpressureStatus.Normal;
    }

    public void UpdateQueueSize(string connectionId, int size)
    {
        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            state.QueueSize = size;
            state.LastUpdated = DateTime.UtcNow;
            
            var utilization = state.UtilizationPercent;
            
            if (utilization >= 95)
            {
                state.Status = BackpressureStatus.Critical;
            }
            else if (utilization >= 80)
            {
                state.Status = BackpressureStatus.High;
            }
            else if (utilization >= 50)
            {
                state.Status = BackpressureStatus.Normal;
            }
            else
            {
                state.Status = BackpressureStatus.Normal;
            }
        }
    }

    public async Task<bool> WaitForCapacityAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var state = _connectionStates.GetValueOrDefault(connectionId);
        if (state == null) return true;
        
        var cfg = _defaultConfig;
        
        for (var retry = 0; retry < cfg.MaxRetries; retry++)
        {
            if (state.Status == BackpressureStatus.Normal)
            {
                return true;
            }
            
            if (state.Status == BackpressureStatus.Blocked)
            {
                await Task.Delay(cfg.RetryDelayMs * (retry + 1), cancellationToken);
            }
        }
        
        return state.Status == BackpressureStatus.Normal;
    }

    public void RemoveConnection(string connectionId)
    {
        if (_queues.TryRemove(connectionId, out var channel))
        {
            channel.Writer.Complete();
        }
        _connectionStates.TryRemove(connectionId, out _);
    }

    public BackpressureState? GetState(string connectionId)
    {
        _connectionStates.TryGetValue(connectionId, out var state);
        return state;
    }

    public ConcurrentDictionary<string, BackpressureState> GetAllStates()
    {
        return _connectionStates;
    }

    public void SetStatus(string connectionId, BackpressureStatus status)
    {
        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            state.Status = status;
            state.LastUpdated = DateTime.UtcNow;
        }
    }

    public BackpressureStatistics GetStatistics()
    {
        var states = _connectionStates.Values.ToList();
        return new BackpressureStatistics
        {
            TotalConnections = states.Count,
            NormalCount = states.Count(s => s.Status == BackpressureStatus.Normal),
            HighCount = states.Count(s => s.Status == BackpressureStatus.High),
            CriticalCount = states.Count(s => s.Status == BackpressureStatus.Critical),
            BlockedCount = states.Count(s => s.Status == BackpressureStatus.Blocked),
            DroppingCount = states.Count(s => s.Status == BackpressureStatus.Dropping),
            AverageUtilization = states.Count > 0 ? states.Average(s => s.UtilizationPercent) : 0,
            MaxUtilization = states.Count > 0 ? states.Max(s => s.UtilizationPercent) : 0,
            Timestamp = DateTime.UtcNow
        };
    }

    public void Reset()
    {
        foreach (var kvp in _queues)
        {
            kvp.Value.Writer.Complete();
        }
        _queues.Clear();
        _connectionStates.Clear();
    }

    private void CleanupInactiveQueues(object? state)
    {
        var inactiveThreshold = DateTime.UtcNow.AddMinutes(-30);
        
        foreach (var kvp in _queues)
        {
            if (_connectionStates.TryGetValue(kvp.Key, out var connectionState) &&
                connectionState.LastUpdated < inactiveThreshold)
            {
                RemoveConnection(kvp.Key);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        
        foreach (var channel in _queues.Values)
        {
            channel.Writer.Complete();
        }
        _queues.Clear();
        _connectionStates.Clear();
    }
}
