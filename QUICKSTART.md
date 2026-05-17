# Flow Designer 快速启动指南

## 前置要求

1. **.NET 8.0 SDK** - 下载并安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Git** -（可选）用于克隆项目

## 快速开始

### 1. 恢复依赖包

在项目根目录执行：

```bash
dotnet restore
```

### 2. 构建项目

```bash
dotnet build
```

### 3. 启动后端 API（终端 1）

```bash
cd src/FlowDesigner.Api
dotnet run --urls "http://localhost:5000"
```

API 将在 http://localhost:5000 启动，可通过 Swagger UI 访问文档：http://localhost:5000/swagger

### 4. 启动前端 Web（终端 2）

```bash
cd src/FlowDesigner.Web
dotnet run --urls "http://localhost:5001"
```

前端应用将在 http://localhost:5001 启动

## 核心功能说明

### 节点类型

Flow Designer 包含以下节点类型：

#### 基础节点
- **注入 (Inject)** - 手动触发流程
- **调试 (Debug)** - 输出消息到控制台
- **函数 (Function)** - 自定义代码执行
- **改变 (Change)** - 修改消息属性
- **开关 (Switch)** - 条件路由
- **延迟 (Delay)** - 消息延迟
- **模板 (Template)** - 模板消息

#### PLC 节点
- **Modbus 读写** - Modbus TCP 通信
- **S7 读写** - 西门子 S7 PLC 通信
- **位操作** - 位运算操作
- **数据转换** - 数据类型转换
- **定时器** - PLC 风格定时器
- **计数器** - PLC 风格计数器
- **PLC 连接** - 连接管理
- **PLC 报警** - 数据异常报警
- **数据记录** - 数据持久化

#### 视觉节点
- **图像输入** - 从文件/URL/摄像头获取图像
- **YOLO 检测** - AI 目标检测
- **图像处理** - OpenCV 图像处理
- **图像输出** - 保存/显示图像
- **图像过滤** - 按条件过滤图像
- **参数微调** - YOLO 参数优化
- **视频分析** - 视频流处理
- **图像预处理** - AI 预处理

### 配置文件

API 配置位于 `src/FlowDesigner.Api/appsettings.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Execution": {
    "MaxConcurrency": 100,      // 最大并发数
    "MaxQueueSize": 10000,     // 队列大小
    "DefaultTimeoutSeconds": 30 // 默认超时（秒）
  },
  "AllowedHosts": "*"
}
```

## 主要 API 端点

### 流程管理

```http
GET     /api/flows              # 获取所有流程
POST    /api/flows              # 创建新流程
GET     /api/flows/{id}         # 获取特定流程
PUT     /api/flows/{id}         # 更新流程
DELETE  /api/flows/{id}         # 删除流程
```

### 节点管理

```http
GET     /api/nodes              # 获取所有节点定义
GET     /api/nodes/{type}       # 获取特定节点类型
```

### 流程执行

```http
POST    /api/execution/flow/{flowId}/run      # 运行流程
POST    /api/execution/flow/{flowId}/node/{nodeId}/run  # 运行单个节点
POST    /api/execution/flow/{flowId}/stop     # 停止流程
GET     /api/execution/flow/{flowId}/status   # 获取状态
```

### 性能监控

```http
GET     /api/execution/statistics        # 获取统计数据
GET     /api/execution/metrics/flows     # 流程指标
GET     /api/execution/metrics/flows/{id} # 特定流程指标
GET     /api/execution/metrics/nodes     # 节点指标
GET     /api/execution/metrics/system    # 系统指标
POST    /api/execution/metrics/reset     # 重置指标
GET     /api/execution/backpressure      # 背压状态
POST    /api/execution/backpressure/reset # 重置背压
```

## 使用示例

### 1. 创建简单流程

```bash
# 创建流程（使用 JSON payload）
curl -X POST http://localhost:5000/api/flows \
  -H "Content-Type: application/json" \
  -d '{
    "name": "测试流程",
    "description": "我的第一个流程",
    "nodes": [
      {
        "id": "node1",
        "type": "inject",
        "properties": {"payload": "Hello!"}
      },
      {
        "id": "node2",
        "type": "debug",
        "properties": {"complete": false}
      }
    ],
    "connections": [
      {"source": "node1", "target": "node2"}
    ]
  }'
```

### 2. 运行流程

```bash
# 运行流程
curl -X POST http://localhost:5000/api/execution/flow/{flowId}/run
```

### 3. 查看统计

```bash
# 获取统计信息
curl http://localhost:5000/api/execution/statistics
```

## 常见问题

### Q: 构建失败？

A: 确保安装了正确的 .NET SDK 版本：

```bash
dotnet --version  # 应该是 8.x.x
```

### Q: API 无法启动？

A: 检查端口占用情况：

```bash
# Linux/macOS
lsof -i :5000

# Windows
netstat -ano | findstr :5000
```

### Q: YOLO 模型在哪？

A: 默认模型文件应放在 `src/FlowDesigner.Api/Models/yolov8n.onnx`。如果没有，系统会使用模拟推理。

### Q: 如何启用 GPU 加速？

A: 需要：
1. 安装 CUDA 驱动
2. 使用 ONNX Runtime GPU 包
3. 在代码中设置 `UseGPU = true`

## 下一步

1. 查看完整文档：`README.md`
2. 生产部署指南：`PRODUCTION_DEPLOYMENT_GUIDE.md`
3. 构建指南：`BUILD_GUIDE.md`

## 技术支持

如遇到问题，请检查日志文件或使用 Swagger UI 进行 API 测试。
