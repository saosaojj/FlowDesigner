# TCP 和 RTP 协议通讯指南

## 概述

Flow Designer 现在支持 TCP 和 RTP 自由协议通讯，包含 4 个节点类型：

- **TCP 客户端 (tcp-client)** - 作为 TCP 客户端连接服务器
- **TCP 服务器 (tcp-server)** - 作为 TCP 服务器监听连接
- **RTP 发送器 (rtp-sender)** - 发送 RTP 数据包
- **RTP 接收器 (rtp-receiver)** - 接收 RTP 数据包

## 新增文件

### 数据模型
- `ProtocolModels.cs` - TCP 和 RTP 相关数据模型

### 服务
- `TcpService.cs` - TCP 通讯服务
- `RtpService.cs` - RTP 通讯服务

### API
- `TcpController` - TCP REST API
- `RtpController` - RTP REST API

## TCP 节点说明

### TCP 客户端 (tcp-client)

**功能**: 作为 TCP 客户端连接服务器，发送和接收数据

**配置项**:
- `host` - 主机地址 (默认: 127.0.0.1)
- `port` - 端口 (默认: 8080)
- `autoReconnect` - 自动重连 (默认: true)
- `reconnectInterval` - 重连间隔毫秒 (默认: 3000)
- `maxReconnectAttempts` - 最大重连次数 (默认: 10)
- `connectionName` - 连接名称 (默认: TCP-Client)
- `useDelimiter` - 使用分隔符 (默认: true)
- `delimiter` - 分隔符 (默认: \n)
- `bufferSize` - 缓冲区大小 (默认: 8192)

**输入端口**:
- `send` - 要发送的数据

**输出端口**:
- `received` - 接收到的数据
- `status` - 连接状态变化

**使用示例**:
```json
{
    "host": "192.168.1.100",
    "port": 9000,
    "autoReconnect": true,
    "useDelimiter": true,
    "delimiter": "\r\n"
}
```

### TCP 服务器 (tcp-server)

**功能**: 作为 TCP 服务器监听连接，支持多客户端

**配置项**:
- `host` - 监听地址 (默认: 0.0.0.0)
- `port` - 监听端口 (默认: 8080)
- `connectionName` - 服务名称 (默认: TCP-Server)
- `useDelimiter` - 使用分隔符 (默认: true)
- `delimiter` - 分隔符 (默认: \n)
- `bufferSize` - 缓冲区大小 (默认: 8192)

**输入端口**:
- `broadcast` - 要广播给所有客户端的数据

**输出端口**:
- `received` - 从任意客户端接收到的数据
- `clientConnected` - 新客户端连接事件
- `clientDisconnected` - 客户端断开事件

## RTP 节点说明

### RTP 发送器 (rtp-sender)

**功能**: 发送 RTP 数据包（实时传输协议）

**配置项**:
- `host` - 目标地址 (默认: 127.0.0.1)
- `port` - 目标端口 (默认: 5004)
- `ssrc` - 同步源标识 (默认: 12345)
- `payloadType` - 负载类型 (默认: 0)
- `clockRate` - 时钟频率 (默认: 8000)
- `multicast` - 是否多播 (默认: false)
- `ttl` - 生存时间 (默认: 64)
- `sessionName` - 会话名称 (默认: RTP-Sender)

**输入端口**:
- `send` - 要发送的数据

**输出端口**:
- `sent` - 已发送的数据包

**常见负载类型**:
- 0 - PCMU (G.711 A-Law, 8kHz)
- 8 - PCMA (G.711 μ-Law, 8kHz)
- 10 - L16, Stereo, 44.1kHz
- 11 - L16, Mono, 44.1kHz
- 96-127 - 动态负载类型

### RTP 接收器 (rtp-receiver)

**功能**: 接收 RTP 数据包

**配置项**:
- `host` - 绑定地址 (默认: 0.0.0.0)
- `port` - 监听端口 (默认: 5004)
- `ssrc` - 预期 SSRC (0 表示任意)
- `payloadType` - 预期负载类型 (-1 表示任意)
- `multicast` - 多播接收 (默认: false)
- `sessionName` - 会话名称 (默认: RTP-Receiver)

**输出端口**:
- `received` - 接收到的数据（负载部分）
- `packet` - 完整的 RTP 包对象

## REST API

### TCP API

#### 获取所有连接
```http
GET /api/tcp/connections
```

#### 获取单个连接
```http
GET /api/tcp/connections/{id}
```

#### 连接
```http
POST /api/tcp/connect?name=MyTCPClient
Content-Type: application/json

{
    "host": "127.0.0.1",
    "port": 8080,
    "isServer": false,
    "autoReconnect": true,
    "reconnectInterval": 3000,
    "maxReconnectAttempts": 10,
    "useDelimiter": true,
    "delimiter": "\n"
}
```

#### 断开连接
```http
POST /api/tcp/connections/{id}/disconnect
```

#### 发送数据
```http
POST /api/tcp/connections/{id}/send
Content-Type: application/octet-stream

<二进制数据>
```

#### 发送字符串
```http
POST /api/tcp/connections/{id}/sendString
Content-Type: application/json

"Hello TCP!"
```

### RTP API

#### 获取所有会话
```http
GET /api/rtp/sessions
```

#### 获取单个会话
```http
GET /api/rtp/sessions/{id}
```

#### 启动会话
```http
POST /api/rtp/start?name=MyRTP
Content-Type: application/json

{
    "host": "127.0.0.1",
    "port": 5004,
    "isSender": true,
    "ssrc": 12345,
    "payloadType": 0,
    "clockRate": 8000,
    "multicast": false,
    "ttl": 64
}
```

#### 停止会话
```http
POST /api/rtp/sessions/{id}/stop
```

#### 发送数据包
```http
POST /api/rtp/sessions/{id}/send?marker=false
Content-Type: application/octet-stream

<二进制数据>
```

## 使用流程示例

### 示例 1: TCP 串口服务器模拟
```
[TCP 服务器] -> [数据解析] -> [调试输出]
       ^
       |
[注入数据] ---> |
```

### 示例 2: RTP 音频流传输
```
[音频文件读取] -> [RTP 发送器] ---网络---> [RTP 接收器] -> [音频播放]
```

### 示例 3: TCP 双向通讯
```
[注入] -> [TCP 客户端] -> (网络) -> [TCP 服务器] -> [处理] -> [TCP 服务器] -> (网络) -> [TCP 客户端] -> [调试]
```

## 连接状态

TCP 连接状态：
- `Disconnected` - 已断开
- `Connecting` - 正在连接
- `Connected` - 已连接
- `Listening` - 正在监听（服务器）
- `Error` - 错误状态

## RTP 包结构

RTP 包包含以下字段：
- `Data` - 负载数据
- `Timestamp` - 时间戳
- `SequenceNumber` - 序列号
- `Ssrc` - 同步源标识
- `PayloadType` - 负载类型
- `Marker` - 标记位
- `ReceivedAt` - 接收时间

## 测试建议

### TCP 测试
1. 使用 `nc` (netcat) 工具进行测试
```bash
# 监听端口
nc -l -p 8080

# 连接服务器
nc 127.0.0.1 8080
```

2. 使用 telnet
```bash
telnet 127.0.0.1 8080
```

### RTP 测试
1. 使用 `ffmpeg` 进行 RTP 流测试
```bash
# 接收 RTP 流（PCMU）
ffplay -f rtp rtp://127.0.0.1:5004
```

2. 使用 Wireshark 抓包分析 RTP 包
