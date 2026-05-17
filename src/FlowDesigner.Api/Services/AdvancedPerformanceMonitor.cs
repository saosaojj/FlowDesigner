using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace FlowDesigner.Api.Services;

public class AdvancedPerformanceMonitor
{
    private readonly ILogger<AdvancedPerformanceMonitor> _logger;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics;
    private readonly ConcurrentDictionary<string, TimingStats> _timingStats;
    private readonly ConcurrentDictionary<string, SystemMetrics> _historicalMetrics;
    private readonly Timer _monitorTimer;
    private readonly object _lockObject = new();

    public SystemMetrics CurrentSystemMetrics { get; private set; } = new();

    public AdvancedPerformanceMonitor(ILogger<AdvancedPerformanceMonitor> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _metrics = new ConcurrentDictionary<string, PerformanceMetrics>();
        _timingStats = new ConcurrentDictionary<string, TimingStats>();
        _historicalMetrics = new ConcurrentDictionary<string, SystemMetrics>();

        // 每 1 秒更新一次系统指标
        _monitorTimer = new Timer(UpdateSystemMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        _logger.LogInformation("高级性能监控器已初始化");
    }

    #region 性能指标收集

    public void RecordExecution(string operationName, TimeSpan duration, bool success = true)
    {
        var stats = _timingStats.GetOrAdd(operationName, _ => new TimingStats());
        stats.Record(duration, success);
    }

    public IDisposable BeginTiming(string operationName)
    {
        return new TimingScope(this, operationName);
    }

    public void UpdateFlowMetrics(string flowId, FlowPerformanceData data)
    {
        var metrics = _metrics.GetOrAdd(flowId, _ => new PerformanceMetrics());
        metrics.Update(data);
    }

    #endregion

    #region 系统指标收集

    private void UpdateSystemMetrics(object? state)
    {
        try
        {
            var metrics = new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GetMemoryUsage(),
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                HandleCount = Process.GetCurrentProcess().HandleCount,
                WorkingSet = Process.GetCurrentProcess().WorkingSet64,
                PrivateMemory = Process.GetCurrentProcess().PrivateMemorySize64,
                VirtualMemory = Process.GetCurrentProcess().VirtualMemorySize64
            };

            CurrentSystemMetrics = metrics;

            // 存储历史指标
            var key = $"system_metrics_{metrics.Timestamp:yyyyMMddHHmmss}";
            _historicalMetrics[key] = metrics;

            // 清理超过 1 小时的历史数据
            var cutoff = DateTime.UtcNow.AddHours(-1);
            var oldKeys = _historicalMetrics.Keys.Where(k =>
            {
                var parts = k.Split('_');
                if (parts.Length >= 3 && DateTime.TryParseExact(parts[2], "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var timestamp))
                {
                    return timestamp < cutoff;
                }
                return false;
            }).ToList();

            foreach (var oldKey in oldKeys)
            {
                _historicalMetrics.TryRemove(oldKey, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系统指标失败");
        }
    }

    private float GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpu = process.TotalProcessorTime;

            Thread.Sleep(100);

            var endTime = DateTime.UtcNow;
            var endCpu = process.TotalProcessorTime;

            var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
            var totalMs = (endTime - startTime).TotalMilliseconds;
            var cpuUsage = (float)(cpuUsedMs / (Environment.ProcessorCount * totalMs) * 100);

            return Math.Max(0, Math.Min(100, cpuUsage));
        }
        catch
        {
            return 0;
        }
    }

    private long GetMemoryUsage()
    {
        try
        {
            return GC.GetTotalMemory(false);
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region 查询方法

    public PerformanceReport GetPerformanceReport(TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromMinutes(5);
        var report = new PerformanceReport
        {
            GeneratedAt = DateTime.UtcNow,
            TimeWindow = window,
            SystemMetrics = CurrentSystemMetrics,
            FlowMetrics = _metrics.ToDictionary(k => k.Key, k => k.Value.GetMetrics()),
            TimingStats = _timingStats.ToDictionary(k => k.Key, k => k.Value.GetStats())
        };

        // 计算整体性能指标
        var allTimings = _timingStats.Values;
        report.AverageLatency = allTimings.Count > 0 ? allTimings.Average(t => t.Average) : 0;
        report.P95Latency = allTimings.Count > 0 ? allTimings.Average(t => t.Percentile95) : 0;
        report.P99Latency = allTimings.Count > 0 ? allTimings.Average(t => t.Percentile99) : 0;
        report.SuccessRate = allTimings.Count > 0 ? allTimings.Average(t => t.SuccessRate) : 0;
        report.Throughput = CalculateThroughput(allTimings, window);

        return report;
    }

    public TimingStatistics GetTimingStatistics(string operationName)
    {
        if (_timingStats.TryGetValue(operationName, out var stats))
        {
            return stats.GetStats();
        }

        return new TimingStatistics();
    }

    public List<SystemMetrics> GetHistoricalSystemMetrics(TimeSpan duration)
    {
        var metrics = new List<SystemMetrics>();
        var startTime = DateTime.UtcNow - duration;

        foreach (var kvp in _historicalMetrics)
        {
            if (kvp.Value.Timestamp >= startTime)
            {
                metrics.Add(kvp.Value);
            }
        }

        return metrics.OrderBy(m => m.Timestamp).ToList();
    }

    #endregion

    #region 辅助方法

    private double CalculateThroughput(IEnumerable<TimingStats> stats, TimeSpan window)
    {
        var totalCount = stats.Sum(s => s.TotalCount);
        var durationSeconds = window.TotalSeconds;
        return durationSeconds > 0 ? totalCount / durationSeconds : 0;
    }

    #endregion

    #region 内部类

    private class TimingScope : IDisposable
    {
        private readonly AdvancedPerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public TimingScope(AdvancedPerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.RecordExecution(_operationName, _stopwatch.Elapsed);
        }
    }

    #endregion
}

public class TimingStats
{
    private readonly ConcurrentQueue<double> _timings = new ConcurrentQueue<double>();
    private int _successCount;
    private int _failureCount;

    public int TotalCount => _successCount + _failureCount;
    public double SuccessRate => TotalCount > 0 ? (double)_successCount / TotalCount * 100 : 100;

    public double Average
    {
        get
        {
            var list = _timings.ToList();
            return list.Count > 0 ? list.Average() : 0;
        }
    }

    public double Percentile50 => GetPercentile(50);
    public double Percentile95 => GetPercentile(95);
    public double Percentile99 => GetPercentile(99);

    public void Record(TimeSpan duration, bool success)
    {
        _timings.Enqueue(duration.TotalMilliseconds);

        // 只保留最近 1000 条记录
        while (_timings.Count > 1000)
        {
            _timings.TryDequeue(out _);
        }

        if (success)
        {
            Interlocked.Increment(ref _successCount);
        }
        else
        {
            Interlocked.Increment(ref _failureCount);
        }
    }

    public TimingStatistics GetStats()
    {
        var list = _timings.ToList();

        return new TimingStatistics
        {
            Count = list.Count,
            Average = list.Count > 0 ? list.Average() : 0,
            Min = list.Count > 0 ? list.Min() : 0,
            Max = list.Count > 0 ? list.Max() : 0,
            Percentile50 = GetPercentile(50),
            Percentile95 = GetPercentile(95),
            Percentile99 = GetPercentile(99),
            SuccessCount = _successCount,
            FailureCount = _failureCount,
            SuccessRate = SuccessRate
        };
    }

    private double GetPercentile(int percentile)
    {
        var list = _timings.OrderBy(x => x).ToList();
        if (list.Count == 0) return 0;

        var index = (int)Math.Ceiling(percentile / 100.0 * list.Count) - 1;
        return list[Math.Max(0, Math.Min(index, list.Count - 1))];
    }
}

public class PerformanceMetrics
{
    public long TotalExecutions { get; private set; }
    public long SuccessCount { get; private set; }
    public long FailureCount { get; private set; }
    public double TotalExecutionTime { get; private set; }
    public double LastExecutionTime { get; private set; }
    public DateTime? LastStartTime { get; private set; }
    public DateTime? LastEndTime { get; private set; }

    private readonly ConcurrentQueue<double> _recentExecutionTimes = new ConcurrentQueue<double>();

    public void Update(FlowPerformanceData data)
    {
        TotalExecutions++;

        if (data.Success)
        {
            SuccessCount++;
        }
        else
        {
            FailureCount++;
        }

        if (data.ExecutionTime.HasValue)
        {
            TotalExecutionTime += data.ExecutionTime.Value.TotalMilliseconds;
            LastExecutionTime = data.ExecutionTime.Value.TotalMilliseconds;
            _recentExecutionTimes.Enqueue(data.ExecutionTime.Value.TotalMilliseconds);

            // 只保留最近 100 条记录
            while (_recentExecutionTimes.Count > 100)
            {
                _recentExecutionTimes.TryDequeue(out _);
            }
        }

        LastStartTime = data.StartTime;
        LastEndTime = data.EndTime;
    }

    public FlowPerformanceMetrics GetMetrics()
    {
        var times = _recentExecutionTimes.ToList();
        return new FlowPerformanceMetrics
        {
            TotalExecutions = TotalExecutions,
            SuccessCount = SuccessCount,
            FailureCount = FailureCount,
            SuccessRate = TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 100,
            AverageExecutionTime = times.Count > 0 ? times.Average() : 0,
            LastExecutionTime = LastExecutionTime,
            MinExecutionTime = times.Count > 0 ? times.Min() : 0,
            MaxExecutionTime = times.Count > 0 ? times.Max() : 0,
            LastStartTime = LastStartTime,
            LastEndTime = LastEndTime
        };
    }
}

public class FlowPerformanceData
{
    public bool Success { get; set; }
    public TimeSpan? ExecutionTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class SystemMetrics
{
    public DateTime Timestamp { get; set; }
    public float CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public long WorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long VirtualMemory { get; set; }
}

public class PerformanceReport
{
    public DateTime GeneratedAt { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public SystemMetrics SystemMetrics { get; set; } = new SystemMetrics();
    public Dictionary<string, FlowPerformanceMetrics> FlowMetrics { get; set; } = new();
    public Dictionary<string, TimingStatistics> TimingStats { get; set; } = new();
    public double AverageLatency { get; set; }
    public double P95Latency { get; set; }
    public double P99Latency { get; set; }
    public double SuccessRate { get; set; }
    public double Throughput { get; set; }
}

public class TimingStatistics
{
    public int Count { get; set; }
    public double Average { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Percentile50 { get; set; }
    public double Percentile95 { get; set; }
    public double Percentile99 { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
}

public class FlowPerformanceMetrics
{
    public long TotalExecutions { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public double AverageExecutionTime { get; set; }
    public double LastExecutionTime { get; set; }
    public double MinExecutionTime { get; set; }
    public double MaxExecutionTime { get; set; }
    public DateTime? LastStartTime { get; set; }
    public DateTime? LastEndTime { get; set; }
}
