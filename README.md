# Flow Designer - Blazor 工业自动化流程编辑器

## 项目简介

基于 Blazor 和 ASP.NET Core 的工业自动化流程编辑器，支持 PLC 通讯、Modbus TCP、西门子 S7 协议，具有高性能并发执行引擎。

## 项目结构

```
FlowDesigner/
├── FlowDesigner.sln                    # 解决方案文件
├── README.md
└── src/
    ├── FlowDesigner.Shared/            # 共享模型库
    │   └── Models/
    │       ├── Flow.cs
    │       ├── FlowConnection.cs
    │       ├── FlowMessage.cs
    │       ├── FlowNode.cs
    │       ├── NodeDefinition.cs
    │       ├── ExecutionModels.cs
    │       ├── PlcModels.cs
    │       └── PlcNodeDefinitions.cs
    ├── FlowDesigner.Api/               # ASP.NET Core 后端 API
    │   ├── Controllers/
    │   │   ├── ExecutionController.cs
    │   │   ├── FlowsController.cs
    │   │   └── NodesController.cs
    │   ├── Services/
    │   │   ├── BackpressureController.cs
    │   │   ├── ExecutionEngine.cs
    │   │   ├── FlowService.cs
    │   │   ├── HighPerformanceExecutionEngine.cs
    │   │   ├── NodeRegistryService.cs
    │   │   ├── PerformanceMonitor.cs
    │   │   └── PlcCommunicationService.cs
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── FlowDesigner.Api.csproj
    └── FlowDesigner.Web/               # Blazor WebAssembly 前端
        ├── Components/
        │   ├── FlowConnection.razor
        │   ├── FlowConnectionLine.razor
        │   └── FlowNodeCard.razor
        ├── Pages/
        │   ├── FlowEditor.razor
        │   ├── FlowEditor.razor.css
        │   ├── Index.razor
        │   └── Index.razor.css
        ├── Services/
        │   └── FlowApiService.cs
        └── Program.cs
```

## 快速开始

### 前置条件

- .NET 8.0 SDK 或更高版本

### 构建项目

```bash
cd FlowDesigner
dotnet build
```

### 启动后端 API（终端 1）

```bash
cd src/FlowDesigner.Api
dotnet run --urls "http://localhost:5000"
```

API 服务将在 http://localhost:5000 上运行，Swagger UI 可在 http://localhost:5000/swagger 访问。

### 启动前端 Web（终端 2）

```bash
cd src/FlowDesigner.Web
dotnet run --urls "http://localhost:5001"
```

前端应用将在 http://localhost:5001 上运行。

## 核心特性

### 1. 高性能并发执行引擎

- 异步非阻塞消息队列
- 线程池管理
- 优先级调度
- 背压控制和流量限制
- 熔断器保护机制
- 实时性能监控

### 2. PLC 通讯支持

#### Modbus TCP
- 读取线圈状态 (01)
- 读取离散输入 (02)
- 读取保持寄存器 (03)
- 读取输入寄存器 (04)
- 写单个线圈 (05)
- 写单个寄存器 (06)
- 写多个寄存器 (16)

#### 西门子 S7
- 支持 S7-1200/1500/300/400
- 读取数据块、标记、输入、输出区域
- 写入操作
- 地址解析 (如 DB1.DBD0)

### 3. 流程节点类型

#### 输入输出
- 注入节点 (inject)
- 调试节点 (debug)

#### 功能节点
- 函数节点 (function)
- 修改节点 (change)
- 开关节点 (switch)
- 延迟节点 (delay)
- 模板节点 (template)

#### PLC 节点
- Modbus 读取 (modbus-read)
- Modbus 写入 (modbus-write)
- S7 读取 (s7-read)
- S7 写入 (s7-write)
- 位操作 (bit-operation)
- 数据转换 (data-convert)
- 定时器 (plc-timer)
- 计数器 (plc-counter)
- PLC 连接 (plc-connect)
- PLC 报警 (plc-alarm)
- 数据记录 (plc-log)

## API 端点

### 流程执行
```
POST /api/execution/flow/{flowId}/run
POST /api/execution/flow/{flowId}/node/{nodeId}/run
POST /api/execution/flow/{flowId}/stop
GET /api/execution/flow/{flowId}/status
```

### 性能监控
```
GET /api/execution/statistics
GET /api/execution/metrics/flows
GET /api/execution/metrics/flows/{flowId}
GET /api/execution/metrics/nodes
GET /api/execution/metrics/nodes/{nodeId}
GET /api/execution/metrics/system
POST /api/execution/metrics/reset
```

### 背压控制
```
GET /api/execution/backpressure
POST /api/execution/backpressure/reset
```

## 配置说明

### 执行引擎配置 (appsettings.json)

```json
{
  "Execution": {
    "MaxConcurrency": 100,
    "MaxQueueSize": 10000,
    "DefaultTimeoutSeconds": 30
  }
}
```

## 性能指标

- 并发执行: 100 流程
- 消息吞吐量: 50,000+ 消息/秒
- 平均延迟: 5ms
- 内存优化: 对象池复用

## 开发说明

### 技术栈
- .NET 8.0
- ASP.NET Core
- Blazor WebAssembly
- System.Threading.Channels (高性能队列)
- 无依赖注入的轻量架构

### 注意事项

1. 项目目前处于开发阶段
2. PLC 通讯功能需要真实硬件设备测试
3. 性能指标为理论值，实际取决于硬件配置

## 许可证

MIT License
