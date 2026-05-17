# Flow Designer 生产环境部署指南

## 📋 目录

1. [项目概述](#项目概述)
2. [系统要求](#系统要求)
3. [安装步骤](#安装步骤)
4. [配置说明](#配置说明)
5. [启动服务](#启动服务)
6. [功能模块说明](#功能模块说明)
7. [性能优化](#性能优化)
8. [常见问题](#常见问题)

---

## 🚀 项目概述

Flow Designer 是一个企业级的工业自动化流程编辑器，包含以下核心功能：

- ✅ **流程编排引擎** - 可视化流程设计和执行
- ✅ **高性能执行引擎** - 支持 100+ 并发流程执行
- ✅ **PLC 通讯模块** - Modbus TCP 和西门子 S7 协议支持
- ✅ **计算机视觉** - YOLO 目标检测 + OpenCV 图像处理
- ✅ **视频流处理** - 实时 RTSP 流分析
- ✅ **性能监控** - 完整的系统指标收集

---

## 💻 系统要求

### 最低配置

- **操作系统**: Windows 10 / Linux (Ubuntu 20.04+) / macOS 10.15+
- **CPU**: 4 核心
- **内存**: 8 GB
- **磁盘**: 20 GB 可用空间
- **.NET**: .NET 8.0 SDK

### 推荐配置（生产环境）

- **操作系统**: Linux (Ubuntu 22.04 LTS)
- **CPU**: 8+ 核心（支持硬件加速）
- **内存**: 32 GB+
- **磁盘**: 100 GB+ SSD
- **GPU**: NVIDIA 显卡（可选，用于加速推理）
- **.NET**: .NET 8.0 SDK
- **Docker**: 20.10+（可选，容器化部署）

---

## 🔧 安装步骤

### 1. 克隆项目

```bash
cd /workspace
git clone <repository-url> FlowDesigner
cd FlowDesigner
```

### 2. 安装 .NET 8.0 SDK

```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0.100

# Windows
# 下载并安装 https://dotnet.microsoft.com/download/dotnet/8.0
```

### 3. 恢复依赖包

```bash
dotnet restore
```

### 4. 下载 YOLO 模型

```bash
mkdir -p /workspace/FlowDesigner/src/FlowDesigner.Api/Models
cd /workspace/FlowDesigner/src/FlowDesigner.Api/Models

# 下载 YOLOv8n 模型 (ONNX 格式)
# 可以从 https://github.com/ultralytics/assets/releases 下载
# 或者使用下面的示例命令：

# wget https://github.com/ultralytics/assets/releases/download/v8.0.0/yolov8n.onnx
```

### 5. 安装 OpenCV 依赖（Linux 系统）

```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y libopencv-dev libgdiplus
```

---

## ⚙️ 配置说明

### appsettings.json 配置

编辑 `/workspace/FlowDesigner/src/FlowDesigner.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Execution": {
    "MaxConcurrency": 100,          // 最大并发执行数
    "MaxQueueSize": 10000,          // 队列最大大小
    "DefaultTimeoutSeconds": 30     // 默认超时时间(秒)
  },
  "AllowedHosts": "*"
}
```

### 环境变量配置

```bash
# API 配置
export ASPNETCORE_URLS="http://0.0.0.0:5000"
export ASPNETCORE_ENVIRONMENT="Production"

# 性能配置
export Execution__MaxConcurrency="200"
export Execution__MaxQueueSize="50000"

# 日志配置
export Logging__LogLevel__Default="Warning"
```

---

## 🚀 启动服务

### 开发环境启动

```bash
# 终端 1 - 启动后端 API
cd /workspace/FlowDesigner/src/FlowDesigner.Api
dotnet run --urls "http://localhost:5000"

# 终端 2 - 启动前端 Web
cd /workspace/FlowDesigner/src/FlowDesigner.Web
dotnet run --urls "http://localhost:5001"
```

### 生产环境部署

#### 1. 发布应用

```bash
# 发布 API
cd /workspace/FlowDesigner/src/FlowDesigner.Api
dotnet publish -c Release -o /app/api

# 发布 Web
cd /workspace/FlowDesigner/src/FlowDesigner.Web
dotnet publish -c Release -o /app/web
```

#### 2. 使用 systemd 托管（Linux）

创建 `/etc/systemd/system/flow-designer-api.service`:

```ini
[Unit]
Description=Flow Designer API
After=network.target

[Service]
User=www-data
WorkingDirectory=/app/api
ExecStart=/usr/bin/dotnet /app/api/FlowDesigner.Api.dll
Restart=always
RestartSec=10

# 环境变量
Environment="ASPNETCORE_URLS=http://0.0.0.0:5000"
Environment="ASPNETCORE_ENVIRONMENT=Production"

[Install]
WantedBy=multi-user.target
```

启动服务：

```bash
sudo systemctl daemon-reload
sudo systemctl enable flow-designer-api
sudo systemctl start flow-designer-api
sudo systemctl status flow-designer-api
```

#### 3. Docker 部署

创建 `Dockerfile` (API):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/FlowDesigner.Api/FlowDesigner.Api.csproj", "FlowDesigner.Api/"]
COPY ["src/FlowDesigner.Shared/FlowDesigner.Shared.csproj", "FlowDesigner.Shared/"]
RUN dotnet restore "FlowDesigner.Api/FlowDesigner.Api.csproj"
COPY . .
WORKDIR "/src/src/FlowDesigner.Api"
RUN dotnet build "FlowDesigner.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FlowDesigner.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# 复制 YOLO 模型文件
COPY --from=publish /src/src/FlowDesigner.Api/Models /app/Models
ENTRYPOINT ["dotnet", "FlowDesigner.Api.dll"]
```

Docker Compose (`docker-compose.yml`):

```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - ./data:/app/data
      - ./Models:/app/Models
    restart: unless-stopped
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
```

---

## 📦 功能模块说明

### 1. YOLO 目标检测模块

**文件位置**: `src/FlowDesigner.Api/Services/RealYoloDetectionService.cs`

**功能特性**:
- ✅ ONNX Runtime 推理
- ✅ GPU 加速支持（CUDA）
- ✅ 80+ COCO 类别识别
- ✅ 参数微调（Fine-tuning）
- ✅ NMS（非极大值抑制）
- ✅ 检测结果可视化

**使用方法**:

```csharp
// 加载模型
await yoloService.LoadModel("/path/to/model.onnx", "default");

// 执行检测
var result = await yoloService.DetectAsync(imageBytes, new YoloDetectionParams
{
    ConfidenceThreshold = 0.5f,
    IoUThreshold = 0.45f,
    MaxDetections = 100,
    UseGPU = true
});

// 绘制检测框
var annotated = yoloService.DrawDetections(imageBytes, result.Objects);
```

**节点类型**:
- `yolo-detect` - YOLO 检测节点
- `yolo-finetune` - 参数微调节点

### 2. OpenCV 图像处理模块

**文件位置**: `src/FlowDesigner.Api/Services/RealImageProcessingService.cs`

**支持操作**:
- ✅ 调整大小（Resize）
- ✅ 裁剪（Crop）
- ✅ 旋转（Rotate）
- ✅ 翻转（Flip）
- ✅ 模糊（Blur/高斯模糊）
- ✅ 锐化（Sharpen）
- ✅ 灰度化（Grayscale）
- ✅ 二值化（Threshold）
- ✅ 边缘检测（Canny）
- ✅ 轮廓检测（Contours）
- ✅ 颜色空间转换
- ✅ 直方图均衡化
- ✅ 形态学操作

**使用方法**:

```csharp
var result = await imageService.ProcessAsync(imageBytes, new ImageProcessingParams
{
    OperationType = ImageOperationType.Blur,
    BlurKernelSize = 5,
    BlurSigmaX = 0.8f
});
```

**节点类型**:
- `image-process` - 图像处理节点
- `image-input` - 图像输入节点
- `image-output` - 图像输出节点
- `image-filter` - 图像过滤节点
- `image-preprocess` - 图像预处理节点

### 3. 视频流处理模块

**文件位置**: `src/FlowDesigner.Api/Services/VideoStreamService.cs`

**支持源**:
- ✅ 本地视频文件
- ✅ RTSP 实时流
- ✅ USB 摄像头
- ✅ 网络摄像头

**功能特性**:
- ✅ 自动重连
- ✅ 帧缓冲管理
- ✅ 跳帧支持
- ✅ 性能监控
- ✅ 流统计信息

**使用方法**:

```csharp
// 启动流
await videoService.StartStreamAsync("stream1", new VideoStreamConfig
{
    SourceType = VideoSourceType.RTSP,
    SourcePathOrUrl = "rtsp://camera-ip:554/stream",
    Fps = 30,
    FrameSkip = 2,
    MaxBufferSize = 100,
    AutoReconnect = true
});

// 获取最新帧
var frame = await videoService.GetLatestFrameAsync("stream1");

// 获取流统计
var stats = await videoService.GetStreamStatsAsync("stream1");

// 停止流
await videoService.StopStreamAsync("stream1");
```

**节点类型**:
- `video-analyze` - 视频分析节点

### 4. PLC 通讯模块

**文件位置**: `src/FlowDesigner.Api/Services/PlcCommunicationService.cs`

**协议支持**:
- ✅ Modbus TCP
- ✅ 西门子 S7 (S7-1200/1500/200/300/400)

**功能特性**:
- ✅ 读写支持
- ✅ 连接池管理
- ✅ 断线重连
- ✅ 批量操作优化

**节点类型**:
- `modbus-read` - Modbus 读取节点
- `modbus-write` - Modbus 写入节点
- `s7-read` - S7 读取节点
- `s7-write` - S7 写入节点
- `plc-connect` - PLC 连接管理节点
- `plc-alarm` - PLC 报警节点
- `plc-log` - 数据记录节点
- `bit-operation` - 位操作节点
- `data-convert` - 数据转换节点
- `plc-timer` - 定时器节点
- `plc-counter` - 计数器节点

### 5. 性能监控模块

**文件位置**: `src/FlowDesigner.Api/Services/AdvancedPerformanceMonitor.cs`

**监控指标**:
- ✅ CPU 使用率
- ✅ 内存占用
- ✅ 执行延迟（P50/P95/P99）
- ✅ 吞吐量统计
- ✅ 成功率统计
- ✅ 历史数据（1 小时）

**使用方法**:

```csharp
// 记录执行时间
using (performanceMonitor.BeginTiming("yolo-detect"))
{
    // 执行操作
}

// 获取性能报告
var report = performanceMonitor.GetPerformanceReport(TimeSpan.FromMinutes(5));

// 获取系统指标历史
var history = performanceMonitor.GetHistoricalSystemMetrics(TimeSpan.FromHours(1));
```

---

## 📊 性能优化建议

### 1. 硬件加速配置

**启用 GPU 推理**:

```csharp
// 在 YoloDetectionParams 中配置
var params = new YoloDetectionParams
{
    UseGPU = true  // 启用 GPU
};
```

**注意**: 确保安装了 CUDA 驱动和 ONNX Runtime GPU 版本

### 2. 内存优化

- 启用对象池
- 配置适当的缓冲区大小
- 及时释放大对象

```json
{
  "Execution": {
    "MaxConcurrency": 200,
    "MaxQueueSize": 50000
  }
}
```

### 3. 网络优化

- 使用本地 PLC 连接
- 优化 RTSP 流参数
- 批量读写操作

### 4. 数据库优化

- 使用 SQLite 或 PostgreSQL
- 定期清理历史数据
- 建立合适的索引

---

## 🔍 API 文档

### Swagger UI

启动 API 后访问：`http://localhost:5000/swagger`

### 主要 API 端点

#### 流程执行

- `POST /api/execution/flow/{flowId}/run` - 启动流程
- `POST /api/execution/flow/{flowId}/stop` - 停止流程
- `GET /api/execution/flow/{flowId}/status` - 获取状态

#### 性能监控

- `GET /api/execution/statistics` - 获取统计信息
- `GET /api/execution/metrics/flows` - 获取流程指标
- `GET /api/execution/metrics/system` - 获取系统指标

#### 视觉服务

- `POST /api/vision/yolo/detect` - YOLO 检测
- `POST /api/vision/image/process` - 图像处理
- `POST /api/vision/video/stream/{streamId}/start` - 启动流
- `GET /api/vision/video/stream/{streamId}/frame` - 获取帧

---

## ⚠️ 常见问题

### Q1: 找不到 YOLO 模型文件？

**A**: 确保模型文件在正确的位置：

```
/workspace/FlowDesigner/src/FlowDesigner.Api/Models/yolov8n.onnx
```

或者修改代码指向正确的路径。

### Q2: OpenCV 初始化失败？

**A**: 安装依赖：

```bash
# Ubuntu/Debian
sudo apt-get install -y libopencv-dev libgdiplus

# CentOS/RHEL
sudo yum install -y opencv opencv-devel
```

### Q3: GPU 加速不工作？

**A**: 检查 CUDA 安装：

```bash
nvcc --version
nvidia-smi
```

确保使用了 ONNX Runtime GPU 版本。

### Q4: RTSP 流连接失败？

**A**: 检查网络连接、用户名密码，以及 RTSP 路径是否正确。可以使用 VLC 等工具测试流是否正常。

### Q5: 如何调整日志级别？

**A**: 修改 `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

---

## 📞 支持与反馈

如遇到问题，请提供以下信息：

1. 系统环境（OS、.NET 版本）
2. 错误日志
3. 复现步骤
4. 配置信息

---

## 📄 许可证

MIT License

---

## ✨ 更新日志

### v1.0.0 (2024-01-15)

- ✅ 初始版本发布
- ✅ 完整的流程编排引擎
- ✅ PLC 通讯模块
- ✅ YOLO 目标检测集成
- ✅ OpenCV 图像处理
- ✅ 视频流处理
- ✅ 高级性能监控
