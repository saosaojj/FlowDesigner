using FlowDesigner.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlowDesigner.Api.Services;

public class DashboardService
{
    private readonly ConcurrentDictionary<string, DashboardConfig> _dashboards = new();
    private readonly ConcurrentDictionary<string, DashboardDataSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<string, AlarmRecord> _alarms = new();
    private readonly ConcurrentDictionary<string, DeviceStatus> _devices = new();
    private readonly ConcurrentDictionary<string, List<RealTimeDataPoint>> _realTimeData = new();
    private readonly HighPerformanceExecutionEngine _engine;
    private readonly PerformanceMonitor _monitor;
    private readonly Random _random = new Random();

    public DashboardService(HighPerformanceExecutionEngine engine, PerformanceMonitor monitor)
    {
        _engine = engine;
        _monitor = monitor;
        InitializeDefaultDashboards();
        InitializeMockDevices();
    }

    private void InitializeDefaultDashboards()
    {
        var defaultDashboard = new DashboardConfig
        {
            Id = "default-dashboard",
            Name = "工业生产总览",
            Description = "工厂生产综合监控大屏",
            IsActive = true,
            RefreshInterval = 2000,
            Layout = new DashboardLayout
            {
                Columns = 12,
                Rows = 8,
                BackgroundColor = "#0f172a",
                Theme = "dark"
            },
            Widgets = new List<DashboardWidget>
            {
                new DashboardWidget
                {
                    Id = "flow-status",
                    Name = "流程运行状态",
                    Type = DashboardWidgetType.StatusIndicator,
                    X = 0,
                    Y = 0,
                    Width = 3,
                    Height = 2,
                    Title = "流程状态",
                    Icon = "fa-project-diagram",
                    Color = "#3b82f6",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "flowStatus"
                    }
                },
                new DashboardWidget
                {
                    Id = "active-flows",
                    Name = "活跃流程数",
                    Type = DashboardWidgetType.NumberCard,
                    X = 3,
                    Y = 0,
                    Width = 3,
                    Height = 2,
                    Title = "活跃流程",
                    Subtitle = "当前运行",
                    Icon = "fa-play-circle",
                    Color = "#10b981",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "activeFlows",
                        Unit = "个"
                    }
                },
                new DashboardWidget
                {
                    Id = "execution-count",
                    Name = "执行总次数",
                    Type = DashboardWidgetType.NumberCard,
                    X = 6,
                    Y = 0,
                    Width = 3,
                    Height = 2,
                    Title = "执行次数",
                    Subtitle = "累计",
                    Icon = "fa-tasks",
                    Color = "#8b5cf6",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "totalExecutions",
                        Unit = "次"
                    }
                },
                new DashboardWidget
                {
                    Id = "success-rate",
                    Name = "成功率",
                    Type = DashboardWidgetType.Gauge,
                    X = 9,
                    Y = 0,
                    Width = 3,
                    Height = 2,
                    Title = "执行成功率",
                    Icon = "fa-check-circle",
                    Color = "#f59e0b",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "successRate",
                        Unit = "%",
                        MinValue = 0,
                        MaxValue = 100,
                        Thresholds = new List<ThresholdConfig>
                        {
                            new ThresholdConfig { Name = "正常", Value = 95, Color = "#10b981", Status = "normal" },
                            new ThresholdConfig { Name = "警告", Value = 85, Color = "#f59e0b", Status = "warning" },
                            new ThresholdConfig { Name = "危险", Value = 0, Color = "#ef4444", Status = "danger" }
                        }
                    }
                },
                new DashboardWidget
                {
                    Id = "cpu-usage",
                    Name = "CPU使用率",
                    Type = DashboardWidgetType.ProgressBar,
                    X = 0,
                    Y = 2,
                    Width = 4,
                    Height = 1,
                    Title = "CPU使用率",
                    Icon = "fa-microchip",
                    Color = "#ef4444",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "cpuUsage",
                        Unit = "%",
                        MinValue = 0,
                        MaxValue = 100
                    }
                },
                new DashboardWidget
                {
                    Id = "memory-usage",
                    Name = "内存使用率",
                    Type = DashboardWidgetType.ProgressBar,
                    X = 4,
                    Y = 2,
                    Width = 4,
                    Height = 1,
                    Title = "内存使用率",
                    Icon = "fa-memory",
                    Color = "#3b82f6",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "memoryUsage",
                        Unit = "%",
                        MinValue = 0,
                        MaxValue = 100
                    }
                },
                new DashboardWidget
                {
                    Id = "queue-depth",
                    Name = "队列深度",
                    Type = DashboardWidgetType.ProgressBar,
                    X = 8,
                    Y = 2,
                    Width = 4,
                    Height = 1,
                    Title = "任务队列",
                    Icon = "fa-stream",
                    Color = "#8b5cf6",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "queueDepth",
                        Unit = "个",
                        MinValue = 0,
                        MaxValue = 1000
                    }
                },
                new DashboardWidget
                {
                    Id = "device-status",
                    Name = "设备状态",
                    Type = DashboardWidgetType.DeviceStatus,
                    X = 0,
                    Y = 3,
                    Width = 4,
                    Height = 3,
                    Title = "设备监控",
                    Icon = "fa-microchip",
                    Color = "#06b6d4",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "devices"
                    }
                },
                new DashboardWidget
                {
                    Id = "real-time-chart",
                    Name = "实时数据",
                    Type = DashboardWidgetType.LineChart,
                    X = 4,
                    Y = 3,
                    Width = 4,
                    Height = 3,
                    Title = "实时数据流",
                    Icon = "fa-chart-line",
                    Color = "#10b981",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "realtime",
                        DataKey = "flowRate"
                    }
                },
                new DashboardWidget
                {
                    Id = "alarm-list",
                    Name = "报警列表",
                    Type = DashboardWidgetType.AlarmList,
                    X = 8,
                    Y = 3,
                    Width = 4,
                    Height = 3,
                    Title = "最近报警",
                    Icon = "fa-exclamation-triangle",
                    Color = "#ef4444",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "alarms"
                    }
                },
                new DashboardWidget
                {
                    Id = "current-time",
                    Name = "系统时间",
                    Type = DashboardWidgetType.CustomText,
                    X = 0,
                    Y = 6,
                    Width = 12,
                    Height = 2,
                    Title = "",
                    Icon = "fa-clock",
                    Color = "#f59e0b",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "system",
                        DataKey = "currentTime"
                    }
                }
            }
        };

        _dashboards[defaultDashboard.Id] = defaultDashboard;

        var plcDashboard = new DashboardConfig
        {
            Id = "plc-dashboard",
            Name = "PLC监控大屏",
            Description = "PLC设备和通信状态监控",
            IsActive = true,
            RefreshInterval = 1000,
            Layout = new DashboardLayout
            {
                Columns = 12,
                Rows = 8,
                BackgroundColor = "#0f172a",
                Theme = "dark"
            },
            Widgets = new List<DashboardWidget>
            {
                new DashboardWidget
                {
                    Id = "modbus-status",
                    Name = "Modbus连接状态",
                    Type = DashboardWidgetType.StatusIndicator,
                    X = 0,
                    Y = 0,
                    Width = 4,
                    Height = 2,
                    Title = "Modbus连接",
                    Icon = "fa-plug",
                    Color = "#10b981",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "plc",
                        DataKey = "modbusStatus"
                    }
                },
                new DashboardWidget
                {
                    Id = "s7-status",
                    Name = "S7连接状态",
                    Type = DashboardWidgetType.StatusIndicator,
                    X = 4,
                    Y = 0,
                    Width = 4,
                    Height = 2,
                    Title = "S7连接",
                    Icon = "fa-plug",
                    Color = "#3b82f6",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "plc",
                        DataKey = "s7Status"
                    }
                },
                new DashboardWidget
                {
                    Id = "plc-data-points",
                    Name = "数据点数量",
                    Type = DashboardWidgetType.NumberCard,
                    X = 8,
                    Y = 0,
                    Width = 4,
                    Height = 2,
                    Title = "数据点",
                    Icon = "fa-database",
                    Color = "#8b5cf6",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "plc",
                        DataKey = "dataPoints",
                        Unit = "个"
                    }
                },
                new DashboardWidget
                {
                    Id = "plc-realtime",
                    Name = "PLC实时数据",
                    Type = DashboardWidgetType.RealTimeData,
                    X = 0,
                    Y = 2,
                    Width = 12,
                    Height = 6,
                    Title = "PLC实时数据采集",
                    Icon = "fa-chart-area",
                    Color = "#f59e0b",
                    DataConfig = new WidgetDataConfig
                    {
                        DataSource = "plc",
                        DataKey = "realtimeValues"
                    }
                }
            }
        };

        _dashboards[plcDashboard.Id] = plcDashboard;
    }

    private void InitializeMockDevices()
    {
        var devices = new List<DeviceStatus>
        {
            new DeviceStatus
            {
                Id = "device-001",
                Name = "传送带A",
                Type = "Conveyor",
                HealthStatus = DeviceHealthStatus.Online,
                Uptime = 99.5,
                LastSeen = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    { "speed", 2.5 },
                    { "temperature", 45.2 },
                    { "status", "running" }
                }
            },
            new DeviceStatus
            {
                Id = "device-002",
                Name = "机械臂B",
                Type = "RobotArm",
                HealthStatus = DeviceHealthStatus.Online,
                Uptime = 98.7,
                LastSeen = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    { "cycleCount", 1245 },
                    { "temperature", 52.1 },
                    { "status", "idle" }
                }
            },
            new DeviceStatus
            {
                Id = "device-003",
                Name = "传感器C",
                Type = "Sensor",
                HealthStatus = DeviceHealthStatus.Warning,
                Uptime = 95.3,
                LastSeen = DateTime.UtcNow.AddMinutes(-2),
                StatusMessage = "信号强度低",
                Metrics = new Dictionary<string, object>
                {
                    { "signalStrength", 65 },
                    { "battery", 45 },
                    { "status", "warning" }
                }
            },
            new DeviceStatus
            {
                Id = "device-004",
                Name = "PLC控制器D",
                Type = "PLC",
                HealthStatus = DeviceHealthStatus.Online,
                Uptime = 99.9,
                LastSeen = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    { "scanRate", 100 },
                    { "temperature", 38.5 },
                    { "status", "running" }
                }
            }
        };

        foreach (var device in devices)
        {
            _devices[device.Id] = device;
        }
    }

    public Task<List<DashboardConfig>> GetAllDashboardsAsync()
    {
        return Task.FromResult(_dashboards.Values.OrderBy(d => d.Name).ToList());
    }

    public Task<DashboardConfig?> GetDashboardAsync(string id)
    {
        _dashboards.TryGetValue(id, out var dashboard);
        return Task.FromResult(dashboard);
    }

    public Task<DashboardConfig> CreateDashboardAsync(DashboardConfig config)
    {
        config.Id = Guid.NewGuid().ToString();
        config.CreatedAt = DateTime.UtcNow;
        _dashboards[config.Id] = config;
        return Task.FromResult(config);
    }

    public Task<DashboardConfig?> UpdateDashboardAsync(string id, DashboardConfig config)
    {
        if (_dashboards.TryGetValue(id, out var existing))
        {
            config.Id = id;
            config.CreatedAt = existing.CreatedAt;
            config.UpdatedAt = DateTime.UtcNow;
            _dashboards[id] = config;
            return Task.FromResult(config);
        }
        return Task.FromResult<DashboardConfig?>(null);
    }

    public Task<bool> DeleteDashboardAsync(string id)
    {
        var removed = _dashboards.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    public Task<DashboardDataSnapshot> GetDashboardDataAsync(string dashboardId)
    {
        var snapshot = new DashboardDataSnapshot
        {
            DashboardId = dashboardId,
            Timestamp = DateTime.UtcNow,
            WidgetData = new Dictionary<string, object>()
        };

        var systemStats = _monitor.GetSystemStatistics();
        var engineStats = _engine.GetStatistics();
        var flowStatuses = GetAllFlowStatuses();

        snapshot.SystemStats = systemStats;
        snapshot.FlowStatuses = flowStatuses;
        snapshot.DeviceStatuses = _devices.Values.ToList();
        snapshot.RecentAlarms = _alarms.Values.OrderByDescending(a => a.Timestamp).Take(10).ToList();

        foreach (var widget in GetDashboardWidgets(dashboardId))
        {
            var data = GenerateWidgetData(widget, systemStats, engineStats, flowStatuses);
            snapshot.WidgetData[widget.Id] = data;
        }

        _snapshots[dashboardId] = snapshot;

        return Task.FromResult(snapshot);
    }

    private List<DashboardWidget> GetDashboardWidgets(string dashboardId)
    {
        if (_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            return dashboard.Widgets;
        }
        return new List<DashboardWidget>();
    }

    private object GenerateWidgetData(DashboardWidget widget, SystemStatistics systemStats, EngineStatistics engineStats, List<FlowRuntimeStatus> flowStatuses)
    {
        return widget.Type switch
        {
            DashboardWidgetType.NumberCard => GenerateNumberCardData(widget, systemStats, engineStats),
            DashboardWidgetType.StatusIndicator => GenerateStatusIndicatorData(widget, systemStats, flowStatuses),
            DashboardWidgetType.Gauge => GenerateGaugeData(widget, systemStats, engineStats),
            DashboardWidgetType.ProgressBar => GenerateProgressBarData(widget, systemStats),
            DashboardWidgetType.LineChart => GenerateLineChartData(widget),
            DashboardWidgetType.BarChart => GenerateBarChartData(widget, flowStatuses),
            DashboardWidgetType.PieChart => GeneratePieChartData(widget, flowStatuses),
            DashboardWidgetType.DeviceStatus => GenerateDeviceStatusData(),
            DashboardWidgetType.AlarmList => GenerateAlarmListData(),
            DashboardWidgetType.RealTimeData => GenerateRealTimeData(widget),
            DashboardWidgetType.CustomText => GenerateCustomTextData(widget),
            _ => new { }
        };
    }

    private NumberCardData GenerateNumberCardData(DashboardWidget widget, SystemStatistics systemStats, EngineStatistics engineStats)
    {
        double value = 0;
        double? change = null;

        switch (widget.DataConfig.DataKey)
        {
            case "activeFlows":
                value = _engine.GetStatistics().ActiveFlows;
                change = _random.NextDouble() * 2 - 1;
                break;
            case "totalExecutions":
                value = _engine.GetStatistics().TotalExecutions;
                change = _random.NextDouble() * 10;
                break;
            case "dataPoints":
                value = 1247;
                change = 45;
                break;
            default:
                value = _random.NextDouble() * 100;
                break;
        }

        return new NumberCardData
        {
            Value = value,
            Unit = widget.DataConfig.Unit,
            Format = widget.DataConfig.Format,
            Change = change,
            ChangeText = change.HasValue ? $"{change.Value:F1}{(change.Value >= 0 ? "↑" : "↓")}" : null,
            IsPositive = change.HasValue && change.Value >= 0
        };
    }

    private StatusIndicatorData GenerateStatusIndicatorData(DashboardWidget widget, SystemStatistics systemStats, List<FlowRuntimeStatus> flowStatuses)
    {
        string status = "normal";
        string statusText = "正常";
        string color = "#10b981";
        string? icon = "fa-check-circle";
        string? message = null;

        switch (widget.DataConfig.DataKey)
        {
            case "flowStatus":
                var runningFlows = flowStatuses.Count(f => f.IsRunning);
                if (runningFlows > 0)
                {
                    status = "normal";
                    statusText = "运行中";
                    color = "#10b981";
                    icon = "fa-play-circle";
                    message = $"{runningFlows} 个流程正在运行";
                }
                else
                {
                    status = "idle";
                    statusText = "空闲";
                    color = "#6b7280";
                    icon = "fa-pause-circle";
                    message = "无运行中流程";
                }
                break;
            case "modbusStatus":
                status = "normal";
                statusText = "已连接";
                color = "#10b981";
                icon = "fa-plug";
                message = "Modbus TCP 连接正常";
                break;
            case "s7Status":
                status = "normal";
                statusText = "已连接";
                color = "#3b82f6";
                icon = "fa-plug";
                message = "S7 连接正常";
                break;
            default:
                status = "normal";
                statusText = "正常";
                color = "#10b981";
                break;
        }

        return new StatusIndicatorData
        {
            Status = status,
            StatusText = statusText,
            Color = color,
            Icon = icon,
            Message = message
        };
    }

    private GaugeData GenerateGaugeData(DashboardWidget widget, SystemStatistics systemStats, EngineStatistics engineStats)
    {
        double value = 0;

        switch (widget.DataConfig.DataKey)
        {
            case "successRate":
                value = engineStats.SuccessRate * 100;
                break;
            case "cpuUsage":
                value = systemStats.CpuUsage;
                break;
            case "memoryUsage":
                value = systemStats.MemoryUsage;
                break;
            default:
                value = _random.NextDouble() * 100;
                break;
        }

        return new GaugeData
        {
            Value = value,
            MinValue = widget.DataConfig.MinValue,
            MaxValue = widget.DataConfig.MaxValue,
            Unit = widget.DataConfig.Unit,
            Format = widget.DataConfig.Format,
            Thresholds = widget.DataConfig.Thresholds
        };
    }

    private ProgressBarData GenerateProgressBarData(DashboardWidget widget, SystemStatistics systemStats)
    {
        double value = 0;
        string color = widget.Color;

        switch (widget.DataConfig.DataKey)
        {
            case "cpuUsage":
                value = systemStats.CpuUsage;
                color = value > 80 ? "#ef4444" : value > 60 ? "#f59e0b" : "#10b981";
                break;
            case "memoryUsage":
                value = systemStats.MemoryUsage;
                color = value > 85 ? "#ef4444" : value > 70 ? "#f59e0b" : "#10b981";
                break;
            case "queueDepth":
                value = Math.Min(systemStats.QueueDepth, 1000);
                color = value > 800 ? "#ef4444" : value > 500 ? "#f59e0b" : "#10b981";
                break;
            default:
                value = _random.NextDouble() * 100;
                break;
        }

        return new ProgressBarData
        {
            Value = value,
            MaxValue = widget.DataConfig.MaxValue,
            Unit = widget.DataConfig.Unit,
            Format = widget.DataConfig.Format,
            Color = color,
            ShowValue = true
        };
    }

    private List<ChartDataPoint> GenerateLineChartData(DashboardWidget widget)
    {
        var data = new List<ChartDataPoint>();
        var now = DateTime.UtcNow;

        for (int i = 19; i >= 0; i--)
        {
            var timestamp = now.AddSeconds(-i * 2);
            var value = 50 + Math.Sin(DateTime.Now.Ticks / 10000000.0 + i) * 30 + _random.NextDouble() * 10;

            data.Add(new ChartDataPoint
            {
                Label = timestamp.ToString("HH:mm:ss"),
                Value = value,
                Timestamp = timestamp
            });
        }

        return data;
    }

    private List<ChartDataPoint> GenerateBarChartData(DashboardWidget widget, List<FlowRuntimeStatus> flowStatuses)
    {
        var data = new List<ChartDataPoint>
        {
            new ChartDataPoint { Label = "成功", Value = flowStatuses.Count(f => f.LastExecutionResult?.Success == true), Color = "#10b981" },
            new ChartDataPoint { Label = "失败", Value = flowStatuses.Count(f => f.LastExecutionResult?.Success == false), Color = "#ef4444" },
            new ChartDataPoint { Label = "运行中", Value = flowStatuses.Count(f => f.IsRunning), Color = "#3b82f6" },
            new ChartDataPoint { Label = "等待中", Value = flowStatuses.Count(f => f.IsWaiting), Color = "#f59e0b" }
        };

        return data;
    }

    private List<ChartDataPoint> GeneratePieChartData(DashboardWidget widget, List<FlowRuntimeStatus> flowStatuses)
    {
        var data = new List<ChartDataPoint>
        {
            new ChartDataPoint { Label = "正常", Value = flowStatuses.Count(f => f.LastExecutionResult?.Success == true), Color = "#10b981" },
            new ChartDataPoint { Label = "异常", Value = flowStatuses.Count(f => f.LastExecutionResult?.Success == false), Color = "#ef4444" }
        };

        return data;
    }

    private List<DeviceStatus> GenerateDeviceStatusData()
    {
        return _devices.Values.ToList();
    }

    private List<AlarmRecord> GenerateAlarmListData()
    {
        return _alarms.Values.OrderByDescending(a => a.Timestamp).Take(10).ToList();
    }

    private List<RealTimeDataPoint> GenerateRealTimeData(DashboardWidget widget)
    {
        var dataKey = widget.DataConfig.DataKey ?? "default";
        
        if (!_realTimeData.TryGetValue(dataKey, out var data))
        {
            data = new List<RealTimeDataPoint>();
            _realTimeData[dataKey] = data;
        }

        var now = DateTime.UtcNow;
        var newValue = 50 + Math.Sin(now.Ticks / 10000000.0) * 30 + _random.NextDouble() * 20;

        data.Add(new RealTimeDataPoint
        {
            Timestamp = now,
            Value = newValue,
            Label = now.ToString("HH:mm:ss")
        });

        if (data.Count > 50)
        {
            data.RemoveAt(0);
        }

        return data.ToList();
    }

    private string GenerateCustomTextData(DashboardWidget widget)
    {
        switch (widget.DataConfig.DataKey)
        {
            case "currentTime":
                return DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss");
            default:
                return "";
        }
    }

    private List<FlowRuntimeStatus> GetAllFlowStatuses()
    {
        var statuses = new List<FlowRuntimeStatus>();
        
        return statuses;
    }

    public Task AddAlarmAsync(AlarmRecord alarm)
    {
        _alarms[alarm.Id] = alarm;
        return Task.CompletedTask;
    }

    public Task AcknowledgeAlarmAsync(string alarmId, string userId)
    {
        if (_alarms.TryGetValue(alarmId, out var alarm))
        {
            alarm.IsAcknowledged = true;
            alarm.AcknowledgedBy = userId;
            alarm.AcknowledgedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task<List<AlarmRecord>> GetRecentAlarmsAsync(int count = 20)
    {
        var alarms = _alarms.Values.OrderByDescending(a => a.Timestamp).Take(count).ToList();
        return Task.FromResult(alarms);
    }

    public Task<List<DashboardTemplate>> GetTemplatesAsync()
    {
        var templates = new List<DashboardTemplate>
        {
            new DashboardTemplate
            {
                Id = "general",
                Name = "通用总览",
                Description = "适合工厂综合监控的基础模板",
                Category = "general",
                IsDefault = true,
                Config = _dashboards["default-dashboard"]
            },
            new DashboardTemplate
            {
                Id = "plc",
                Name = "PLC监控",
                Description = "专注于PLC通信和工业设备监控",
                Category = "industrial",
                Config = _dashboards["plc-dashboard"]
            },
            new DashboardTemplate
            {
                Id = "vision",
                Name = "视觉系统监控",
                Description = "监控机器视觉和图像处理系统",
                Category = "vision",
                Config = new DashboardConfig
                {
                    Name = "视觉系统监控",
                    Widgets = new List<DashboardWidget>()
                }
            }
        };

        return Task.FromResult(templates);
    }

    public Task UpdateDeviceStatusAsync(DeviceStatus device)
    {
        _devices[device.Id] = device;
        return Task.CompletedTask;
    }

    public Task<List<DeviceStatus>> GetAllDevicesAsync()
    {
        return Task.FromResult(_devices.Values.ToList());
    }
}
