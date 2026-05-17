# Flow Designer 项目构建指南

## 项目概述

Flow Designer 是一个基于 Blazor 和 .NET Core 的工业自动化流程编辑器，支持：
- ✅ 流程编排和执行
- ✅ PLC 通讯（Modbus TCP、西门子 S7）
- ✅ 视觉处理（YOLO 目标检测、OpenCV 图像处理）
- ✅ 高性能并发执行引擎
- ✅ 完整的 RESTful API

## 项目结构

```
FlowDesigner/
├── FlowDesigner.sln                    # 解决方案文件
├── README.md                           # 项目说明
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
    │       ├── PlcNodeDefinitions.cs
    │       ├── VisionModels.cs         # ✅ 新增
    │       └── VisionNodeDefinitions.cs # ✅ 新增
    ├── FlowDesigner.Api/              # ASP.NET Core 后端
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
    │   │   ├── PlcCommunicationService.cs
    │   │   ├── YoloDetectionService.cs     # ✅ 新增
    │   │   └── ImageProcessingService.cs   # ✅ 新增
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── appsettings.Development.json
    └── FlowDesigner.Web/              # Blazor WebAssembly 前端
        ├── Components/
        ├── Pages/
        ├── Services/
        └── Program.cs
```

## 前置条件

- **.NET 8.0 SDK** 或更高版本
- 推荐使用 Visual Studio 2022 或 VS Code

## 构建步骤

### 1. 恢复依赖

```bash
cd FlowDesigner
dotnet restore
```

### 2. 构建项目

```bash
dotnet build
```

如果遇到错误，请查看下面的"常见问题"部分。

### 3. 运行项目

#### 启动后端 API

```bash
# 终端 1
cd src/FlowDesigner.Api
dotnet run --urls "http://localhost:5000"
```

API 将在 http://localhost:5000 启动
- Swagger UI: http://localhost:5000/swagger
- API 文档: http://localhost:5000/swagger/index.html

#### 启动前端 Web

```bash
# 终端 2
cd src/FlowDesigner.Web
dotnet run --urls "http://localhost:5001"
```

前端应用将在 http://localhost:5001 启动

## 验证构建成功

### 测试 API 端点

```bash
# 获取所有节点定义
curl http://localhost:5000/api/nodes

# 获取所有流程
curl http://localhost:5000/api/flows

# 获取执行引擎统计
curl http://localhost:5000/api/execution/statistics
```

### 预期输出

API 应该返回 JSON 格式的数据，包含所有注册的节点和流程信息。

## 常见问题及解决方案

### 问题 1: 找不到类型或命名空间

**症状：**
```
error CS0246: The type or namespace name 'xxx' could not be found
```

**解决方案：**
1. 确保所有 using 语句正确
2. 重新恢复依赖：`dotnet restore`
3. 清理并重新构建：
```bash
dotnet clean
dotnet build
```

### 问题 2: 重复的类型定义

**症状：**
```
error CS0101: The namespace 'xxx' already contains a definition for 'yyy'
```

**解决方案：**
检查并删除重复的类型定义。在本项目中：
- `ExecutionResult` 定义在 `ExecutionModels.cs` 中
- `FlowMessage.cs` 中不应包含 `ExecutionResult`

### 问题 3: 缺少 System.TimeSpan

**症状：**
```
error CS0246: The type or namespace name 'TimeSpan' could not be found
```

**解决方案：**
在 `VisionModels.cs` 文件顶部添加：
```csharp
using System;
```

### 问题 4: 依赖注入错误

**症状：**
```
error CS0012: The type 'xxx' is defined in an assembly that is not referenced
```

**解决方案：**
1. 确保项目引用正确
2. 重新构建解决方案：
```bash
dotnet build --no-incremental
```

## 项目组件说明

### 1. 共享模型库 (FlowDesigner.Shared)

包含所有共享的类型定义：
- **Flow.cs** - 流程数据模型
- **FlowNode.cs** - 流程节点模型
- **FlowConnection.cs** - 节点连接模型
- **NodeDefinition.cs** - 节点类型定义
- **VisionModels.cs** - 视觉模块模型
- **VisionNodeDefinitions.cs** - 视觉节点定义
- **PlcModels.cs** - PLC 通讯模型
- **PlcNodeDefinitions.cs** - PLC 节点定义

### 2. 后端 API (FlowDesigner.Api)

提供 RESTful API 和业务逻辑：
- **Controllers/** - API 控制器
- **Services/** - 业务逻辑服务
  - 高性能执行引擎
  - 性能监控
  - 背压控制
  - YOLO 检测
  - 图像处理
  - PLC 通讯

### 3. 前端 Web (FlowDesigner.Web)

Blazor WebAssembly 应用：
- **Pages/** - 页面组件
- **Components/** - 可复用组件
- **Services/** - API 服务调用

## 功能模块

### 基础节点
- 注入 (inject)
- 调试 (debug)
- 函数 (function)
- 改变 (change)
- 开关 (switch)
- 延迟 (delay)
- 模板 (template)
- HTTP 输入/输出

### PLC 节点
- Modbus TCP 读取/写入
- 西门子 S7 读取/写入
- 位操作
- 数据转换
- 定时器
- 计数器
- PLC 连接管理
- 报警监控
- 数据记录

### 视觉节点 ✅ 新增
- 图像输入（文件、URL、摄像头、RTSP）
- YOLO 目标检测
- 图像处理（缩放、裁剪、旋转、模糊等）
- 图像输出（保存、显示）
- 图像过滤
- 参数微调
- 视频分析
- 图像预处理

## 配置说明

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Execution": {
    "MaxConcurrency": 100,
    "MaxQueueSize": 10000,
    "DefaultTimeoutSeconds": 30
  },
  "AllowedHosts": "*"
}
```

### CORS 配置

API 配置了允许所有来源的 CORS 策略：
- 允许所有源
- 允许所有方法
- 允许所有请求头

在生产环境中，请根据需要修改 `Program.cs` 中的 CORS 配置。

## 性能特性

- ✅ 异步非阻塞执行
- ✅ 线程池管理
- ✅ 背压控制和流量限制
- ✅ 熔断器保护机制
- ✅ 实时性能监控
- ✅ 对象池和资源复用

## 开发建议

### 添加新的节点类型

1. 在 `FlowDesigner.Shared/Models/` 中定义节点模型
2. 在对应的 `NodeDefinitions.cs` 中添加节点定义
3. 在 `NodeRegistryService.cs` 中注册节点
4. 在执行引擎中添加处理逻辑

### 添加新的服务

1. 在 `FlowDesigner.Api/Services/` 中创建服务类
2. 在 `Program.cs` 中注册服务
3. 注入到需要的控制器或服务中

### 添加新的 API 端点

1. 在 `Controllers/` 中创建或扩展控制器
2. 添加适当的 HTTP 方法和路由
3. 实现业务逻辑

## 测试

项目支持单元测试和集成测试。使用以下命令运行测试：

```bash
dotnet test
```

## 部署

### Docker 部署

```dockerfile
# 后端 API
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY src/FlowDesigner.Api/.
EXPOSE 5000
ENTRYPOINT ["dotnet", "FlowDesigner.Api.dll"]
```

### Kubernetes 部署

项目可以容器化并部署到 Kubernetes 集群：
- 使用 ConfigMap 管理配置
- 使用 Secrets 管理敏感信息
- 配置健康检查和探针
- 设置资源限制和请求

## 许可证

MIT License

## 联系方式

如有问题或建议，请提交 Issue 或 Pull Request。

## 更新日志

### v1.0.0 (2024-01-15)
- ✅ 完成基础流程编辑器
- ✅ 实现高性能执行引擎
- ✅ 添加 PLC 通讯模块
- ✅ 添加视觉处理模块
- ✅ 实现 YOLO 目标检测
- ✅ 实现 OpenCV 图像处理
- ✅ 添加性能监控
- ✅ 添加背压控制
