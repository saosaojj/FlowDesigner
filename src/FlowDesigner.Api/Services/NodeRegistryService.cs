using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class NodeRegistryService
{
    private readonly Dictionary<string, NodeDefinition> _nodeDefinitions = new();

    public NodeRegistryService()
    {
        InitializeBuiltInNodes();
        InitializeVisionNodes();
        InitializePlcNodes();
        InitializeWebSocketNodes();
        InitializeProtocolNodes();
        InitializeDataNodes();
    }

    public Task<List<NodeDefinition>> GetAllNodeDefinitionsAsync()
    {
        return Task.FromResult(_nodeDefinitions.Values.ToList());
    }

    public Task<NodeDefinition?> GetNodeDefinitionAsync(string type)
    {
        _nodeDefinitions.TryGetValue(type, out var definition);
        return Task.FromResult(definition);
    }

    private void InitializeBuiltInNodes()
    {
        _nodeDefinitions["inject"] = new NodeDefinition
        {
            Type = "inject",
            Category = "input",
            Name = "注入",
            Description = "手动触发消息",
            Color = "#4ade80",
            Icon = "fa-play",
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["payload"] = new PropertyDefinition { Type = "string", Label = "载荷", DefaultValue = "Hello World" },
                ["topic"] = new PropertyDefinition { Type = "string", Label = "主题", DefaultValue = "" }
            }
        };

        _nodeDefinitions["debug"] = new NodeDefinition
        {
            Type = "debug",
            Category = "output",
            Name = "调试",
            Description = "输出消息到调试控制台",
            Color = "#f87171",
            Icon = "fa-bug",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["complete"] = new PropertyDefinition { Type = "boolean", Label = "完整消息", DefaultValue = false }
            }
        };

        _nodeDefinitions["function"] = new NodeDefinition
        {
            Type = "function",
            Category = "function",
            Name = "函数",
            Description = "运行自定义函数代码",
            Color = "#fbbf24",
            Icon = "fa-code",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["code"] = new PropertyDefinition { Type = "code", Label = "代码", DefaultValue = "return msg;" }
            }
        };

        _nodeDefinitions["change"] = new NodeDefinition
        {
            Type = "change",
            Category = "function",
            Name = "改变",
            Description = "修改消息属性",
            Color = "#a78bfa",
            Icon = "fa-sliders",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["rules"] = new PropertyDefinition { Type = "array", Label = "规则", DefaultValue = new List<object>() }
            }
        };

        _nodeDefinitions["switch"] = new NodeDefinition
        {
            Type = "switch",
            Category = "function",
            Name = "开关",
            Description = "根据条件路由消息",
            Color = "#a78bfa",
            Icon = "fa-random",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "output1", Type = "any", Label = "输出1" },
                new PortDefinition { Name = "output2", Type = "any", Label = "输出2" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["property"] = new PropertyDefinition { Type = "string", Label = "属性", DefaultValue = "payload" }
            }
        };

        _nodeDefinitions["delay"] = new NodeDefinition
        {
            Type = "delay",
            Category = "function",
            Name = "延迟",
            Description = "延迟消息处理",
            Color = "#22d3ee",
            Icon = "fa-clock",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["timeout"] = new PropertyDefinition { Type = "number", Label = "延迟时间 (毫秒)", DefaultValue = 1000 }
            }
        };

        _nodeDefinitions["template"] = new NodeDefinition
        {
            Type = "template",
            Category = "function",
            Name = "模板",
            Description = "使用模板创建消息",
            Color = "#fb923c",
            Icon = "fa-file-alt",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["template"] = new PropertyDefinition { Type = "textarea", Label = "模板内容", DefaultValue = "" }
            }
        };

        _nodeDefinitions["http-in"] = new NodeDefinition
        {
            Type = "http-in",
            Category = "network",
            Name = "HTTP 输入",
            Description = "HTTP 端点",
            Color = "#3b82f6",
            Icon = "fa-globe",
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["url"] = new PropertyDefinition { Type = "string", Label = "URL", Required = true },
                ["method"] = new PropertyDefinition { Type = "select", Label = "请求方法", DefaultValue = "GET" }
            }
        };

        _nodeDefinitions["http-out"] = new NodeDefinition
        {
            Type = "http-out",
            Category = "network",
            Name = "HTTP 输出",
            Description = "HTTP 响应",
            Color = "#3b82f6",
            Icon = "fa-reply",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["statusCode"] = new PropertyDefinition { Type = "number", Label = "状态码", DefaultValue = 200 }
            }
        };
    }

    private void InitializeVisionNodes()
    {
        try
        {
            var visionNodes = VisionNodeDefinitions.GetAllVisionNodes();
            foreach (var node in visionNodes)
            {
                _nodeDefinitions[node.Type] = node;
            }
        }
        catch
        {
        }
    }

    private void InitializePlcNodes()
    {
        _nodeDefinitions["modbus-read"] = new NodeDefinition
        {
            Type = "modbus-read",
            Category = "plc",
            Name = "Modbus 读取",
            Description = "通过 Modbus TCP 协议读取 PLC 数据",
            Color = "#2563eb",
            Icon = "fa-cloud-download-alt",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "trigger", Type = "any", Label = "触发" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.100" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 502 },
                ["slaveId"] = new PropertyDefinition { Type = "number", Label = "从站ID", DefaultValue = 1 }
            }
        };

        _nodeDefinitions["modbus-write"] = new NodeDefinition
        {
            Type = "modbus-write",
            Category = "plc",
            Name = "Modbus 写入",
            Description = "通过 Modbus TCP 协议写入数据到 PLC",
            Color = "#059669",
            Icon = "fa-cloud-upload-alt",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "trigger", Type = "any", Label = "触发" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.100" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 502 }
            }
        };

        _nodeDefinitions["s7-read"] = new NodeDefinition
        {
            Type = "s7-read",
            Category = "plc",
            Name = "S7 读取",
            Description = "通过西门子 S7 协议读取 PLC 数据",
            Color = "#7c3aed",
            Icon = "fa-download",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "trigger", Type = "any", Label = "触发" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.1" }
            }
        };

        _nodeDefinitions["s7-write"] = new NodeDefinition
        {
            Type = "s7-write",
            Category = "plc",
            Name = "S7 写入",
            Description = "通过西门子 S7 协议写入数据到 PLC",
            Color = "#dc2626",
            Icon = "fa-upload",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "trigger", Type = "any", Label = "触发" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.1" }
            }
        };

        _nodeDefinitions["bit-operation"] = new NodeDefinition
        {
            Type = "bit-operation",
            Category = "plc",
            Name = "位操作",
            Description = "对数据进行位操作（与、或、异或、非）",
            Color = "#ea580c",
            Icon = "fa-microchip",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } }
        };

        _nodeDefinitions["data-convert"] = new NodeDefinition
        {
            Type = "data-convert",
            Category = "plc",
            Name = "数据转换",
            Description = "数据类型转换（INT转REAL，BCD转换等）",
            Color = "#0891b2",
            Icon = "fa-exchange-alt",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition> { new PortDefinition { Name = "output", Type = "any", Label = "输出" } }
        };

        _nodeDefinitions["plc-timer"] = new NodeDefinition
        {
            Type = "plc-timer",
            Category = "plc",
            Name = "定时器",
            Description = "实现 PLC 风格的定时器功能",
            Color = "#4f46e5",
            Icon = "fa-stopwatch",
            Inputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "start", Type = "any", Label = "启动" },
                new PortDefinition { Name = "reset", Type = "any", Label = "复位" }
            },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "done", Type = "any", Label = "完成" },
                new PortDefinition { Name = "elapsed", Type = "any", Label = "已用时间" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["timerType"] = new PropertyDefinition { Type = "select", Label = "定时器类型" },
                ["delay"] = new PropertyDefinition { Type = "number", Label = "延迟时间 (ms)", DefaultValue = 1000 }
            }
        };

        _nodeDefinitions["plc-counter"] = new NodeDefinition
        {
            Type = "plc-counter",
            Category = "plc",
            Name = "计数器",
            Description = "实现 PLC 风格的计数器功能",
            Color = "#be185d",
            Icon = "fa-sort-numeric-up",
            Inputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "countUp", Type = "any", Label = "加计数" },
                new PortDefinition { Name = "countDown", Type = "any", Label = "减计数" },
                new PortDefinition { Name = "reset", Type = "any", Label = "复位" }
            },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "output", Type = "any", Label = "输出" },
                new PortDefinition { Name = "done", Type = "any", Label = "完成" }
            }
        };

        _nodeDefinitions["plc-connect"] = new NodeDefinition
        {
            Type = "plc-connect",
            Category = "plc",
            Name = "PLC 连接",
            Description = "建立和管理 PLC 通讯连接",
            Color = "#059669",
            Icon = "fa-plug",
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "connected", Type = "any", Label = "已连接" },
                new PortDefinition { Name = "disconnected", Type = "any", Label = "已断开" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "IP 地址", DefaultValue = "192.168.1.1" },
                ["protocol"] = new PropertyDefinition { Type = "select", Label = "协议" }
            }
        };

        _nodeDefinitions["plc-alarm"] = new NodeDefinition
        {
            Type = "plc-alarm",
            Category = "plc",
            Name = "PLC 报警",
            Description = "PLC 数据异常报警监控",
            Color = "#dc2626",
            Icon = "fa-bell",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "normal", Type = "any", Label = "正常" },
                new PortDefinition { Name = "warning", Type = "any", Label = "警告" },
                new PortDefinition { Name = "alarm", Type = "any", Label = "报警" }
            }
        };

        _nodeDefinitions["plc-log"] = new NodeDefinition
        {
            Type = "plc-log",
            Category = "plc",
            Name = "数据记录",
            Description = "记录 PLC 数据到数据库或文件",
            Color = "#0891b2",
            Icon = "fa-database",
            Inputs = new List<PortDefinition> { new PortDefinition { Name = "input", Type = "any", Label = "输入" } },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["storageType"] = new PropertyDefinition { Type = "select", Label = "存储类型" },
                ["tableName"] = new PropertyDefinition { Type = "string", Label = "表名/文件名", DefaultValue = "plc_data" }
            }
        };
    }

    private void InitializeWebSocketNodes()
    {
        _nodeDefinitions["websocket-in"] = new NodeDefinition
        {
            Type = "websocket-in",
            Category = "network",
            Name = "WebSocket 输入",
            Description = "从 WebSocket 连接接收消息",
            Color = "#7c3aed",
            Icon = "fa-plug-circle-bolt",
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "output", Type = "any", Label = "消息" },
                new PortDefinition { Name = "status", Type = "any", Label = "状态" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["url"] = new PropertyDefinition { Type = "string", Label = "WebSocket URL", DefaultValue = "ws://localhost:8080" },
                ["autoReconnect"] = new PropertyDefinition { Type = "boolean", Label = "自动重连", DefaultValue = true }
            }
        };

        _nodeDefinitions["websocket-out"] = new NodeDefinition
        {
            Type = "websocket-out",
            Category = "network",
            Name = "WebSocket 输出",
            Description = "通过 WebSocket 连接发送消息",
            Color = "#7c3aed",
            Icon = "fa-plug-circle-check",
            Inputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "input", Type = "any", Label = "输入" }
            },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                new PortDefinition { Name = "error", Type = "any", Label = "错误" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["url"] = new PropertyDefinition { Type = "string", Label = "WebSocket URL", DefaultValue = "ws://localhost:8080" }
            }
        };

        _nodeDefinitions["websocket-server"] = new NodeDefinition
        {
            Type = "websocket-server",
            Category = "network",
            Name = "WebSocket 服务",
            Description = "WebSocket 服务器，接受客户端连接",
            Color = "#7c3aed",
            Icon = "fa-server",
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "connection", Type = "any", Label = "新连接" },
                new PortDefinition { Name = "message", Type = "any", Label = "收到消息" },
                new PortDefinition { Name = "disconnection", Type = "any", Label = "断开连接" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["path"] = new PropertyDefinition { Type = "string", Label = "路径", DefaultValue = "/ws" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 8080 }
            }
        };
    }

    private void InitializeProtocolNodes()
    {
        _nodeDefinitions["tcp-client"] = new NodeDefinition
        {
            Type = "tcp-client",
            Category = "network",
            Name = "TCP 客户端",
            Description = "作为 TCP 客户端连接服务器",
            Color = "#0ea5e9",
            Icon = "fa-plug",
            Inputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "send", Type = "any", Label = "发送数据" }
            },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "received", Type = "any", Label = "接收数据" },
                new PortDefinition { Name = "status", Type = "any", Label = "状态变化" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["host"] = new PropertyDefinition { Type = "string", Label = "主机地址", DefaultValue = "127.0.0.1" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 8080 }
            }
        };

        _nodeDefinitions["tcp-server"] = new NodeDefinition
        {
            Type = "tcp-server",
            Category = "network",
            Name = "TCP 服务器",
            Description = "作为 TCP 服务器监听连接",
            Color = "#0ea5e9",
            Icon = "fa-server",
            Inputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "broadcast", Type = "any", Label = "广播数据" }
            },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "received", Type = "any", Label = "接收数据" },
                new PortDefinition { Name = "clientConnected", Type = "any", Label = "客户端连接" },
                new PortDefinition { Name = "clientDisconnected", Type = "any", Label = "客户端断开" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["host"] = new PropertyDefinition { Type = "string", Label = "监听地址", DefaultValue = "0.0.0.0" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "监听端口", DefaultValue = 8080 }
            }
        };

        _nodeDefinitions["rtp-sender"] = new NodeDefinition
        {
            Type = "rtp-sender",
            Category = "network",
            Name = "RTP 发送器",
            Description = "发送 RTP 数据包",
            Color = "#10b981",
            Icon = "fa-broadcast-tower",
            Inputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "send", Type = "any", Label = "发送数据" }
            },
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "sent", Type = "any", Label = "已发送" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["host"] = new PropertyDefinition { Type = "string", Label = "目标地址", DefaultValue = "127.0.0.1" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "目标端口", DefaultValue = 5004 }
            }
        };

        _nodeDefinitions["rtp-receiver"] = new NodeDefinition
        {
            Type = "rtp-receiver",
            Category = "network",
            Name = "RTP 接收器",
            Description = "接收 RTP 数据包",
            Color = "#10b981",
            Icon = "fa-satellite-dish",
            Outputs = new List<PortDefinition>
            {
                new PortDefinition { Name = "received", Type = "any", Label = "接收数据" },
                new PortDefinition { Name = "packet", Type = "any", Label = "原始包" }
            },
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["host"] = new PropertyDefinition { Type = "string", Label = "绑定地址", DefaultValue = "0.0.0.0" },
                ["port"] = new PropertyDefinition { Type = "number", Label = "监听端口", DefaultValue = 5004 }
            }
        };
    }

    private void InitializeDataNodes()
    {
        var dataNodes = DataNodeDefinitions.GetAllDataNodes();
        foreach (var node in dataNodes)
        {
            _nodeDefinitions[node.Type] = node;
        }
    }
}
