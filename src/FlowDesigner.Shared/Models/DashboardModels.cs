using System;
using System.Collections.Generic;

namespace FlowDesigner.Shared.Models;

// 大屏配置
public class DashboardConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int RefreshInterval { get; set; } = 2000; // 毫秒
    public List<DashboardWidget> Widgets { get; set; } = new();
    public DashboardLayout Layout { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

// 大屏布局
public class DashboardLayout
{
    public int Columns { get; set; } = 12;
    public int Rows { get; set; } = 8;
    public string BackgroundColor { get; set; } = "#0f172a";
    public string Theme { get; set; } = "dark";
    public bool ShowGrid { get; set; } = false;
}

// 大屏组件类型
public enum DashboardWidgetType
{
    Gauge,
    Chart,
    NumberCard,
    StatusIndicator,
    DataTable,
    LineChart,
    BarChart,
    PieChart,
    ProgressBar,
    RealTimeData,
    FlowDisplay,
    DeviceStatus,
    AlarmList,
    CustomText
}

// 大屏组件
public class DashboardWidget
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DashboardWidgetType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 3;
    public int Height { get; set; } = 2;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Icon { get; set; } = "fa-tachometer-alt";
    public string Color { get; set; } = "#3b82f6";
    public WidgetDataConfig DataConfig { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
}

// 组件数据配置
public class WidgetDataConfig
{
    public string? DataSource { get; set; } // 数据源
    public string? DataKey { get; set; } // 数据键
    public string? Format { get; set; } // 格式化字符串
    public string? Unit { get; set; } // 单位
    public double MinValue { get; set; }
    public double MaxValue { get; set; } = 100;
    public List<ThresholdConfig> Thresholds { get; set; } = new();
}

// 阈值配置
public class ThresholdConfig
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Color { get; set; } = "#10b981";
    public string Status { get; set; } = "normal"; // normal, warning, danger
}

// 大屏数据快照
public class DashboardDataSnapshot
{
    public string DashboardId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> WidgetData { get; set; } = new();
    public SystemStatistics? SystemStats { get; set; }
    public List<FlowRuntimeStatus>? FlowStatuses { get; set; }
    public List<DeviceStatus>? DeviceStatuses { get; set; }
    public List<AlarmRecord>? RecentAlarms { get; set; }
}

// 设备状态
public class DeviceStatus
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DeviceHealthStatus HealthStatus { get; set; }
    public double Uptime { get; set; } // 百分比
    public string? StatusMessage { get; set; }
    public DateTime LastSeen { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

// 设备健康状态
public enum DeviceHealthStatus
{
    Online,
    Offline,
    Warning,
    Error,
    Maintenance
}

// 报警记录
public class AlarmRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FlowId { get; set; } = string.Empty;
    public string? FlowName { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string? NodeName { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

// 报警级别
public enum AlarmSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

// 数字卡片组件数据
public class NumberCardData
{
    public double Value { get; set; }
    public string? Unit { get; set; }
    public string? Format { get; set; }
    public double? Change { get; set; } // 变化量
    public string? ChangeText { get; set; }
    public bool IsPositive { get; set; }
}

// 图表数据点
public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double? SecondaryValue { get; set; }
    public string? Color { get; set; }
    public DateTime? Timestamp { get; set; }
}

// 状态指示器数据
public class StatusIndicatorData
{
    public string Status { get; set; } = "normal";
    public string StatusText { get; set; } = "正常";
    public string Color { get; set; } = "#10b981";
    public string? Icon { get; set; }
    public string? Message { get; set; }
}

// 仪表盘数据
public class GaugeData
{
    public double Value { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public string? Unit { get; set; }
    public string? Format { get; set; }
    public List<ThresholdConfig> Thresholds { get; set; } = new();
}

// 进度条数据
public class ProgressBarData
{
    public double Value { get; set; }
    public double MaxValue { get; set; } = 100;
    public string? Unit { get; set; }
    public string? Format { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public bool ShowValue { get; set; } = true;
}

// 实时数据流数据
public class RealTimeDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
}

// 大屏预设模板
public class DashboardTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "general";
    public DashboardConfig Config { get; set; } = new();
    public bool IsDefault { get; set; }
}
