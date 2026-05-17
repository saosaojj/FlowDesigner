using System.Collections.Generic;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public static class DataNodeDefinitions
{
    public static List<NodeDefinition> GetAllDataNodes()
    {
        var nodes = new List<NodeDefinition>();

        nodes.AddRange(GetJsonNodes());
        nodes.AddRange(GetFormatNodes());
        nodes.AddRange(GetArrayNodes());
        nodes.AddRange(GetStringNodes());
        nodes.AddRange(GetCalculationNodes());
        nodes.AddRange(GetBatchNodes());
        nodes.AddRange(GetTimeNodes());

        return nodes;
    }

    private static List<NodeDefinition> GetJsonNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "json",
                Category = "data",
                Name = "JSON",
                Description = "JSON 字符串与对象互转",
                Color = "#f59e0b",
                Icon = "fa-code",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["action"] = new PropertyDefinition { Type = "select", Label = "操作", DefaultValue = "parse" },
                    ["property"] = new PropertyDefinition { Type = "string", Label = "属性", DefaultValue = "payload" }
                }
            }
        };
    }

    private static List<NodeDefinition> GetFormatNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "csv",
                Category = "data",
                Name = "CSV",
                Description = "CSV 数据解析和生成",
                Color = "#10b981",
                Icon = "fa-table",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["action"] = new PropertyDefinition { Type = "select", Label = "操作", DefaultValue = "parse" },
                    ["separator"] = new PropertyDefinition { Type = "string", Label = "分隔符", DefaultValue = "," }
                }
            },
            new NodeDefinition
            {
                Type = "xml",
                Category = "data",
                Name = "XML",
                Description = "XML 数据解析和生成",
                Color = "#8b5cf6",
                Icon = "fa-file-code",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["action"] = new PropertyDefinition { Type = "select", Label = "操作", DefaultValue = "parse" }
                }
            },
            new NodeDefinition
            {
                Type = "yaml",
                Category = "data",
                Name = "YAML",
                Description = "YAML 数据解析和生成",
                Color = "#06b6d4",
                Icon = "fa-file-alt",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["action"] = new PropertyDefinition { Type = "select", Label = "操作", DefaultValue = "parse" }
                }
            }
        };
    }

    private static List<NodeDefinition> GetArrayNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "split",
                Category = "data",
                Name = "分割",
                Description = "将消息分割成多个消息",
                Color = "#ec4899",
                Icon = "fa-columns",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["mode"] = new PropertyDefinition { Type = "select", Label = "分割模式", DefaultValue = "split" },
                    ["separator"] = new PropertyDefinition { Type = "string", Label = "分隔符", DefaultValue = "," },
                    ["chunkSize"] = new PropertyDefinition { Type = "number", Label = "块大小", DefaultValue = 1 }
                }
            },
            new NodeDefinition
            {
                Type = "join",
                Category = "data",
                Name = "合并",
                Description = "将多个消息合并成一个",
                Color = "#f43f5e",
                Icon = "fa-object-group",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["mode"] = new PropertyDefinition { Type = "select", Label = "合并模式", DefaultValue = "auto" },
                    ["separator"] = new PropertyDefinition { Type = "string", Label = "分隔符", DefaultValue = "," },
                    ["count"] = new PropertyDefinition { Type = "number", Label = "消息数量", DefaultValue = 0 }
                }
            },
            new NodeDefinition
            {
                Type = "sort",
                Category = "data",
                Name = "排序",
                Description = "对数组进行排序",
                Color = "#6366f1",
                Icon = "fa-sort-amount-up",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["key"] = new PropertyDefinition { Type = "string", Label = "排序键", DefaultValue = "" },
                    ["order"] = new PropertyDefinition { Type = "select", Label = "排序方向", DefaultValue = "asc" }
                }
            },
            new NodeDefinition
            {
                Type = "filter",
                Category = "data",
                Name = "过滤",
                Description = "根据条件过滤数组",
                Color = "#14b8a6",
                Icon = "fa-filter",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" },
                    new PortDefinition { Name = "filtered", Type = "any", Label = "被过滤" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["key"] = new PropertyDefinition { Type = "string", Label = "属性", DefaultValue = "" },
                    ["operator"] = new PropertyDefinition { Type = "select", Label = "运算符", DefaultValue = "contains" },
                    ["value"] = new PropertyDefinition { Type = "string", Label = "值", DefaultValue = "" }
                }
            }
        };
    }

    private static List<NodeDefinition> GetStringNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "string",
                Category = "data",
                Name = "字符串",
                Description = "字符串操作和转换",
                Color = "#84cc16",
                Icon = "fa-font",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["operation"] = new PropertyDefinition { Type = "select", Label = "操作", DefaultValue = "uppercase" },
                    ["search"] = new PropertyDefinition { Type = "string", Label = "搜索", DefaultValue = "" },
                    ["replace"] = new PropertyDefinition { Type = "string", Label = "替换", DefaultValue = "" }
                }
            },
            new NodeDefinition
            {
                Type = "regex",
                Category = "data",
                Name = "正则表达式",
                Description = "正则表达式匹配和提取",
                Color = "#a855f7",
                Icon = "fa-search",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "匹配" },
                    new PortDefinition { Name = "nomatch", Type = "any", Label = "不匹配" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["pattern"] = new PropertyDefinition { Type = "string", Label = "正则表达式", DefaultValue = "" },
                    ["flags"] = new PropertyDefinition { Type = "string", Label = "标志", DefaultValue = "" },
                    ["replace"] = new PropertyDefinition { Type = "string", Label = "替换", DefaultValue = "" }
                }
            }
        };
    }

    private static List<NodeDefinition> GetCalculationNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "calculate",
                Category = "data",
                Name = "计算",
                Description = "数学和统计计算",
                Color = "#ef4444",
                Icon = "fa-calculator",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "结果" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["operation"] = new PropertyDefinition { Type = "select", Label = "操作", DefaultValue = "add" },
                    ["value"] = new PropertyDefinition { Type = "string", Label = "值", DefaultValue = "0" },
                    ["roundTo"] = new PropertyDefinition { Type = "number", Label = "保留小数位", DefaultValue = 2 }
                }
            },
            new NodeDefinition
            {
                Type = "range",
                Category = "data",
                Name = "范围映射",
                Description = "数值范围映射和缩放",
                Color = "#f97316",
                Icon = "fa-arrows-alt-h",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["minIn"] = new PropertyDefinition { Type = "number", Label = "输入最小值", DefaultValue = 0 },
                    ["maxIn"] = new PropertyDefinition { Type = "number", Label = "输入最大值", DefaultValue = 100 },
                    ["minOut"] = new PropertyDefinition { Type = "number", Label = "输出最小值", DefaultValue = 0 },
                    ["maxOut"] = new PropertyDefinition { Type = "number", Label = "输出最大值", DefaultValue = 100 }
                }
            },
            new NodeDefinition
            {
                Type = "statistics",
                Category = "data",
                Name = "统计分析",
                Description = "数据统计分析",
                Color = "#06b6d4",
                Icon = "fa-chart-bar",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "结果" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["mode"] = new PropertyDefinition { Type = "select", Label = "统计类型", DefaultValue = "full" },
                    ["property"] = new PropertyDefinition { Type = "string", Label = "属性", DefaultValue = "" }
                }
            }
        };
    }

    private static List<NodeDefinition> GetBatchNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "batch",
                Category = "data",
                Name = "批处理",
                Description = "消息批处理分组",
                Color = "#8b5cf6",
                Icon = "fa-layer-group",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "批次" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["mode"] = new PropertyDefinition { Type = "select", Label = "模式", DefaultValue = "count" },
                    ["count"] = new PropertyDefinition { Type = "number", Label = "批次大小", DefaultValue = 10 },
                    ["interval"] = new PropertyDefinition { Type = "number", Label = "间隔(毫秒)", DefaultValue = 1000 }
                }
            },
            new NodeDefinition
            {
                Type = "throttle",
                Category = "data",
                Name = "限流",
                Description = "消息限流控制",
                Color = "#eab308",
                Icon = "fa-tachometer-alt",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["rate"] = new PropertyDefinition { Type = "number", Label = "速率(条/秒)", DefaultValue = 1 },
                    ["capacity"] = new PropertyDefinition { Type = "number", Label = "容量", DefaultValue = 100 }
                }
            }
        };
    }

    private static List<NodeDefinition> GetTimeNodes()
    {
        return new List<NodeDefinition>
        {
            new NodeDefinition
            {
                Type = "timestamp",
                Category = "data",
                Name = "时间戳",
                Description = "获取或格式化时间戳",
                Color = "#64748b",
                Icon = "fa-clock",
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["format"] = new PropertyDefinition { Type = "string", Label = "格式", DefaultValue = "ISO8601" },
                    ["add"] = new PropertyDefinition { Type = "number", Label = "增加(秒)", DefaultValue = 0 }
                }
            },
            new NodeDefinition
            {
                Type = "trigger",
                Category = "data",
                Name = "触发器",
                Description = "定时或循环触发消息",
                Color = "#0ea5e9",
                Icon = "fa-stopwatch",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "reset", Type = "any", Label = "重置" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "output", Type = "any", Label = "输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["loop"] = new PropertyDefinition { Type = "boolean", Label = "循环", DefaultValue = false },
                    ["interval"] = new PropertyDefinition { Type = "number", Label = "间隔(毫秒)", DefaultValue = 1000 },
                    ["payload"] = new PropertyDefinition { Type = "string", Label = "负载", DefaultValue = "1" }
                }
            }
        };
    }
}
