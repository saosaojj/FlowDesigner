namespace FlowDesigner.Shared.Models;

public static class PlcNodeDefinitions
{
    public static List<NodeDefinition> GetAllPlcNodes()
    {
        return new List<NodeDefinition>
        {
            // Modbus TCP 读取节点
            new NodeDefinition
            {
                Type = "modbus-read",
                Category = "plc",
                Name = "Modbus 读取",
                Description = "通过 Modbus TCP 协议读取 PLC 数据",
                Color = "#2563eb",
                Icon = "fa-cloud-download-alt",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "trigger", Type = "any", Label = "触发" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                    new PortDefinition { Name = "error", Type = "any", Label = "错误" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["connection"] = new PropertyDefinition { Type = "string", Label = "连接配置", Required = true },
                    ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.100" },
                    ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 502 },
                    ["slaveId"] = new PropertyDefinition { Type = "number", Label = "从站ID", DefaultValue = 1 },
                    ["functionCode"] = new PropertyDefinition { Type = "select", Label = "功能码" },
                    ["address"] = new PropertyDefinition { Type = "string", Label = "起始地址", DefaultValue = "0" },
                    ["count"] = new PropertyDefinition { Type = "number", Label = "读取数量", DefaultValue = 1 }
                }
            },

            // Modbus TCP 写入节点
            new NodeDefinition
            {
                Type = "modbus-write",
                Category = "plc",
                Name = "Modbus 写入",
                Description = "通过 Modbus TCP 协议写入数据到 PLC",
                Color = "#059669",
                Icon = "fa-cloud-upload-alt",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "trigger", Type = "any", Label = "触发" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                    new PortDefinition { Name = "error", Type = "any", Label = "错误" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["connection"] = new PropertyDefinition { Type = "string", Label = "连接配置", Required = true },
                    ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.100" },
                    ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 502 },
                    ["slaveId"] = new PropertyDefinition { Type = "number", Label = "从站ID", DefaultValue = 1 },
                    ["functionCode"] = new PropertyDefinition { Type = "select", Label = "功能码" },
                    ["address"] = new PropertyDefinition { Type = "string", Label = "起始地址", DefaultValue = "0" },
                    ["value"] = new PropertyDefinition { Type = "string", Label = "写入值", DefaultValue = "" }
                }
            },

            // S7 读取节点
            new NodeDefinition
            {
                Type = "s7-read",
                Category = "plc",
                Name = "S7 读取",
                Description = "通过西门子 S7 协议读取 PLC 数据",
                Color = "#7c3aed",
                Icon = "fa-download",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "trigger", Type = "any", Label = "触发" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                    new PortDefinition { Name = "error", Type = "any", Label = "错误" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["connection"] = new PropertyDefinition { Type = "string", Label = "连接配置", Required = true },
                    ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.1" },
                    ["rack"] = new PropertyDefinition { Type = "number", Label = "机架号", DefaultValue = 0 },
                    ["slot"] = new PropertyDefinition { Type = "number", Label = "槽号", DefaultValue = 1 },
                    ["area"] = new PropertyDefinition { Type = "select", Label = "数据区域" },
                    ["address"] = new PropertyDefinition { Type = "string", Label = "DB块.字节.位", DefaultValue = "DB1.DBD0" },
                    ["dataType"] = new PropertyDefinition { Type = "select", Label = "数据类型" }
                }
            },

            // S7 写入节点
            new NodeDefinition
            {
                Type = "s7-write",
                Category = "plc",
                Name = "S7 写入",
                Description = "通过西门子 S7 协议写入数据到 PLC",
                Color = "#dc2626",
                Icon = "fa-upload",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "trigger", Type = "any", Label = "触发" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "success", Type = "any", Label = "成功" },
                    new PortDefinition { Name = "error", Type = "any", Label = "错误" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["connection"] = new PropertyDefinition { Type = "string", Label = "连接配置", Required = true },
                    ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "PLC IP 地址", DefaultValue = "192.168.1.1" },
                    ["rack"] = new PropertyDefinition { Type = "number", Label = "机架号", DefaultValue = 0 },
                    ["slot"] = new PropertyDefinition { Type = "number", Label = "槽号", DefaultValue = 1 },
                    ["area"] = new PropertyDefinition { Type = "select", Label = "数据区域" },
                    ["address"] = new PropertyDefinition { Type = "string", Label = "DB块.字节.位", DefaultValue = "DB1.DBD0" },
                    ["dataType"] = new PropertyDefinition { Type = "select", Label = "数据类型" },
                    ["value"] = new PropertyDefinition { Type = "string", Label = "写入值" }
                }
            },

            // 位操作节点
            new NodeDefinition
            {
                Type = "bit-operation",
                Category = "plc",
                Name = "位操作",
                Description = "对数据进行位操作（与、或、异或、非）",
                Color = "#ea580c",
                Icon = "fa-microchip",
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
                    ["operation"] = new PropertyDefinition { Type = "select", Label = "操作" },
                    ["bitPosition"] = new PropertyDefinition { Type = "number", Label = "位位置 (0-31)", DefaultValue = 0 },
                    ["mask"] = new PropertyDefinition { Type = "string", Label = "掩码 (十六进制)", DefaultValue = "0xFF" }
                }
            },

            // 数据转换节点
            new NodeDefinition
            {
                Type = "data-convert",
                Category = "plc",
                Name = "数据转换",
                Description = "数据类型转换（INT转REAL，BCD转换等）",
                Color = "#0891b2",
                Icon = "fa-exchange-alt",
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
                    ["conversion"] = new PropertyDefinition { Type = "select", Label = "转换类型" },
                    ["sourceFormat"] = new PropertyDefinition { Type = "select", Label = "源格式" },
                    ["targetFormat"] = new PropertyDefinition { Type = "select", Label = "目标格式" }
                }
            },

            // 定时器节点
            new NodeDefinition
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
                    ["delay"] = new PropertyDefinition { Type = "number", Label = "延迟时间 (ms)", DefaultValue = 1000 },
                    ["preset"] = new PropertyDefinition { Type = "number", Label = "预设值", DefaultValue = 1000 }
                }
            },

            // 计数器节点
            new NodeDefinition
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
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["counterType"] = new PropertyDefinition { Type = "select", Label = "计数器类型" },
                    ["preset"] = new PropertyDefinition { Type = "number", Label = "预设值", DefaultValue = 10 },
                    ["initialValue"] = new PropertyDefinition { Type = "number", Label = "初始值", DefaultValue = 0 }
                }
            },

            // PLC 连接管理节点
            new NodeDefinition
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
                    ["connectionName"] = new PropertyDefinition { Type = "string", Label = "连接名称", DefaultValue = "PLC1" },
                    ["protocol"] = new PropertyDefinition { Type = "select", Label = "协议" },
                    ["ipAddress"] = new PropertyDefinition { Type = "string", Label = "IP 地址", DefaultValue = "192.168.1.1" },
                    ["port"] = new PropertyDefinition { Type = "number", Label = "端口", DefaultValue = 502 },
                    ["rack"] = new PropertyDefinition { Type = "number", Label = "机架号", DefaultValue = 0 },
                    ["slot"] = new PropertyDefinition { Type = "number", Label = "槽号", DefaultValue = 1 },
                    ["autoConnect"] = new PropertyDefinition { Type = "boolean", Label = "自动连接", DefaultValue = true }
                }
            },

            // 报警节点
            new NodeDefinition
            {
                Type = "plc-alarm",
                Category = "plc",
                Name = "PLC 报警",
                Description = "PLC 数据异常报警监控",
                Color = "#dc2626",
                Icon = "fa-bell",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "input", Type = "any", Label = "输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "normal", Type = "any", Label = "正常" },
                    new PortDefinition { Name = "warning", Type = "any", Label = "警告" },
                    new PortDefinition { Name = "alarm", Type = "any", Label = "报警" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["mode"] = new PropertyDefinition { Type = "select", Label = "监控模式" },
                    ["highHigh"] = new PropertyDefinition { Type = "string", Label = "高高限", DefaultValue = "" },
                    ["high"] = new PropertyDefinition { Type = "string", Label = "高限", DefaultValue = "" },
                    ["low"] = new PropertyDefinition { Type = "string", Label = "低限", DefaultValue = "" },
                    ["lowLow"] = new PropertyDefinition { Type = "string", Label = "低低限", DefaultValue = "" }
                }
            },

            // 数据记录节点
            new NodeDefinition
            {
                Type = "plc-log",
                Category = "plc",
                Name = "数据记录",
                Description = "记录 PLC 数据到数据库或文件",
                Color = "#0891b2",
                Icon = "fa-database",
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
                    ["storageType"] = new PropertyDefinition { Type = "select", Label = "存储类型" },
                    ["tableName"] = new PropertyDefinition { Type = "string", Label = "表名/文件名", DefaultValue = "plc_data" },
                    ["fields"] = new PropertyDefinition { Type = "textarea", Label = "字段映射" },
                    ["interval"] = new PropertyDefinition { Type = "number", Label = "记录间隔 (ms)", DefaultValue = 1000 }
                }
            }
        };
    }
}
