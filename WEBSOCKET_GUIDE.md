# WebSocket 使用指南

## 概述

Flow Designer 现在支持 WebSocket 通讯，包括三个节点类型：

1. **WebSocket 输入** (websocket-in) - 从 WebSocket 连接接收消息
2. **WebSocket 输出** (websocket-out) - 通过 WebSocket 连接发送消息
3. **WebSocket 服务** (websocket-server) - 作为 WebSocket 服务器，接受客户端连接

## 新增文件

### 数据模型
- `WebSocketModels.cs` - WebSocket 相关数据模型

### 服务
- `WebSocketService.cs` - WebSocket 连接管理服务

### API
- `WebSocketController.cs` - WebSocket REST API (包含在 NodesController.cs 中)

## 节点说明

### 1. WebSocket 输入 (websocket-in)

**功能**: 从 WebSocket 服务器接收消息

**配置项**:
- `url` - WebSocket 服务器地址 (例如: ws://localhost:8080)
- `autoReconnect` - 是否自动重连 (默认: true)
- `reconnectInterval` - 重连间隔毫秒数 (默认: 3000)
- `connectionName` - 连接名称 (默认: WebSocket1)
- `messageType` - 消息类型 (text/json/binary/auto, 默认: auto)

**输出端口**:
- `output` - 接收到的消息
- `status` - 连接状态变化

**使用示例**:
```json
{
  "url": "ws://echo.websocket.org",
  "autoReconnect": true,
  "reconnectInterval": 3000
}
```

### 2. WebSocket 输出 (websocket-out)

**功能**: 向 WebSocket 服务器发送消息

**配置项**:
- `url` - WebSocket 服务器地址
- `autoReconnect` - 是否自动连接 (默认: true)
- `connectionName` - 连接名称
- `messageType` - 消息类型
- `reuseConnection` - 是否复用连接 (默认: true)

**输入端口**:
- `input` - 要发送的消息

**输出端口**:
- `success` - 发送成功的消息
- `error` - 发送失败的错误信息

### 3. WebSocket 服务 (websocket-server)

**功能**: 创建 WebSocket 服务器，接受客户端连接

**配置项**:
- `path` - WebSocket 路径 (默认: /ws)
- `port` - 监听端口 (默认: 8080)
- `maxConnections` - 最大连接数 (默认: 100)
- `allowCors` - 是否允许 CORS (默认: true)

**输出端口**:
- `connection` - 新客户端连接
- `message` - 收到的消息
- `disconnection` - 客户端断开连接

## REST API

### 获取所有连接
```http
GET /api/websocket/connections
```

### 获取单个连接
```http
GET /api/websocket/connections/{id}
```

### 建立连接
```http
POST /api/websocket/connect?name=MyConnection
Content-Type: application/json

{
  "url": "ws://localhost:8080",
  "autoReconnect": true,
  "reconnectInterval": 3000,
  "maxReconnectAttempts": 10
}
```

### 断开连接
```http
POST /api/websocket/connections/{id}/disconnect
```

### 发送消息
```http
POST /api/websocket/connections/{id}/send
Content-Type: application/json

{
  "type": "text",
  "payload": "Hello WebSocket!"
}
```

## 使用流程示例

### 示例 1: 接收并转发消息

```
[WebSocket 输入] → [处理节点] → [WebSocket 输出]
```

**配置**:
1. WebSocket 输入节点连接到 `ws://data.example.com`
2. 处理节点过滤或转换数据
3. WebSocket 输出节点发送到 `ws://output.example.com`

### 示例 2: 创建聊天服务器

```
[WebSocket 服务] → [处理消息] → [WebSocket 输出 (广播)]
```

**配置**:
1. WebSocket 服务节点监听 8080 端口
2. 处理消息节点处理聊天内容
3. WebSocket 输出节点向所有连接广播消息

## 连接状态

WebSocket 连接有以下状态:
- `Disconnected` - 已断开
- `Connecting` - 正在连接
- `Connected` - 已连接
- `Reconnecting` - 正在重连
- `Error` - 错误状态

## 消息格式

### 文本消息
```json
{
  "type": "text",
  "payload": "Hello, World!",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### JSON 消息
```json
{
  "type": "json",
  "payload": { "key": "value", "number": 123 },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 二进制消息
```json
{
  "type": "binary",
  "payload": [0, 1, 2, 3],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## 测试建议

可以使用以下工具测试:
- **WebSocket Echo Server**: `ws://echo.websocket.org`
- **wscat** (Node.js 工具): `wscat -c ws://localhost:8080`
- **Postman**: 支持 WebSocket 请求
