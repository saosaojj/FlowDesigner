# 数据处理与计算节点指南

## 概述

Flow Designer 现在提供了完整的数据处理和计算节点套件，支持 JSON、CSV、XML、YAML 等格式的解析和转换，以及强大的数学计算和统计分析功能。

## 新增节点清单

### 数据处理节点 (15个)

| 节点类型 | 名称 | 类别 | 功能 |
|---------|------|------|------|
| `json` | JSON | data | JSON 字符串与对象互转 |
| `csv` | CSV | data | CSV 数据解析和生成 |
| `xml` | XML | data | XML 数据解析和生成 |
| `yaml` | YAML | data | YAML 数据解析和生成 |
| `split` | 分割 | data | 将消息分割成多个消息 |
| `join` | 合并 | data | 将多个消息合并成一个 |
| `sort` | 排序 | data | 对数组进行排序 |
| `filter` | 过滤 | data | 根据条件过滤数组 |
| `string` | 字符串 | data | 字符串操作和转换 |
| `regex` | 正则表达式 | data | 正则表达式匹配和提取 |
| `batch` | 批处理 | data | 消息批处理分组 |
| `throttle` | 限流 | data | 消息限流控制 |
| `calculate` | 计算 | data | 数学和统计计算 |
| `range` | 范围映射 | data | 数值范围映射和缩放 |
| `statistics` | 统计分析 | data | 数据统计分析 |

### 时间处理节点 (2个)

| 节点类型 | 名称 | 功能 |
|---------|------|------|
| `timestamp` | 时间戳 | 获取或格式化时间戳 |
| `trigger` | 触发器 | 定时或循环触发消息 |

## 新增文件

### 服务层
- `DataProcessingService.cs` - 数据处理服务
- `DataCalculationService.cs` - 数据计算服务
- `DataNodeDefinitions.cs` - 数据节点定义

## JSON 节点

### 功能
- **Parse**: JSON 字符串 → 对象
- **Stringify**: 对象 → JSON 字符串
- **Format**: 格式化 JSON (缩进)
- **Minify**: 压缩 JSON (去除空白)

### 配置
```json
{
  "action": "parse",    // parse, stringify, format, minify
  "property": "payload"  // 要处理的属性
}
```

### 示例

**输入**:
```json
{"name": "Alice", "age": 30}
```

**Parse 操作后**:
```json
{
  "name": "Alice",
  "age": 30
}
```

## CSV 节点

### 功能
- **Parse**: CSV 字符串 → 数组
- **Stringify**: 数组 → CSV 字符串
- **ToObjects**: CSV → 对象数组

### 配置
```json
{
  "action": "parse",
  "separator": ","        // 分隔符
  "columns": ["name", "age"]
}
```

### 示例

**输入** (CSV):
```
name,age
Alice,30
Bob,25
```

**Parse 操作后**:
```json
[
  {"name": "Alice", "age": "30"},
  {"name": "Bob", "age": "25"}
]
```

## XML 节点

### 功能
- **Parse**: XML → 对象
- **ToJson**: XML → JSON
- **FromJson**: JSON → XML

### 示例

**输入** (XML):
```xml
<person>
  <name>Alice</name>
  <age>30</age>
</person>
```

**Parse 操作后**:
```json
{
  "person": {
    "name": "Alice",
    "age": "30"
  }
}
```

## YAML 节点

### 功能
- **Parse**: YAML → 对象
- **Stringify**: 对象 → YAML
- **ToJson**: YAML → JSON
- **FromJson**: JSON → YAML

### 示例

**输入** (YAML):
```yaml
name: Alice
age: 30
hobbies:
  - reading
  - swimming
```

**Parse 操作后**:
```json
{
  "name": "Alice",
  "age": 30,
  "hobbies": ["reading", "swimming"]
}
```

## 分割节点 (Split)

### 功能
将数组或字符串分割成多个消息

### 配置
```json
{
  "mode": "split",        // split, splitArray
  "separator": ",",       // 分隔符 (字符串模式)
  "chunkSize": 1          // 块大小 (数组模式)
}
```

### 示例

**输入**:
```
a,b,c,d,e
```

**Split 操作后** (5 个消息):
```
"a"
"b"
"c"
"d"
"e"
```

## 合并节点 (Join)

### 功能
将多个消息合并成一个

### 配置
```json
{
  "mode": "auto",        // auto, manual, reduce
  "separator": ",",        // 分隔符
  "count": 0              // 消息数量 (0 = 所有消息)
}
```

### 示例

**输入** (3 条消息):
```
"a"
"b"
"c"
```

**Join 操作后**:
```
"a,b,c"
```

## 排序节点 (Sort)

### 配置
```json
{
  "key": "name",          // 排序键 (空字符串按值排序)
  "order": "asc"          // asc, desc
}
```

### 示例

**输入**:
```json
[
  {"name": "Charlie", "age": 35},
  {"name": "Alice", "age": 30},
  {"name": "Bob", "age": 25}
]
```

**Sort by name (asc)**:
```json
[
  {"name": "Alice", "age": 30},
  {"name": "Bob", "age": 25},
  {"name": "Charlie", "age": 35}
]
```

## 过滤节点 (Filter)

### 配置
```json
{
  "key": "status",        // 要检查的属性
  "operator": "eq",       // eq, ne, gt, lt, gte, lte, contains
  "value": "active"       // 比较的值
}
```

### 示例

**输入**:
```json
[
  {"name": "Alice", "status": "active"},
  {"name": "Bob", "status": "inactive"},
  {"name": "Charlie", "status": "active"}
]
```

**Filter status = "active"**:
```json
[
  {"name": "Alice", "status": "active"},
  {"name": "Charlie", "status": "active"}
]
```

## 字符串节点

### 功能
- **Uppercase**: 转换为大写
- **Lowercase**: 转换为小写
- **Trim**: 去除首尾空格
- **Replace**: 字符串替换
- **Substring**: 截取子串
- **PadLeft/PadRight**: 填充字符串
- **Reverse**: 反转字符串
- **Length**: 获取长度

### 配置
```json
{
  "operation": "replace",
  "search": "old",
  "replace": "new"
}
```

## 正则表达式节点

### 功能
- 正则表达式匹配
- 模式提取
- 替换操作

### 配置
```json
{
  "pattern": "\\d+",      // 正则表达式
  "flags": "g",            // 标志 (g=全局, i=忽略大小写)
  "replace": "NUMBER"      // 替换文本
}
```

### 示例

**输入**: `"Order 12345 shipped"`

**Match `\\d+`**:
```json
{
  "matches": ["12345"],
  "groups": []
}
```

## 计算节点 (Calculate)

### 功能
- **数学运算**: +, -, *, /, %, ^, sqrt, abs, round, floor, ceiling
- **三角函数**: sin, cos, tan, asin, acos, atan
- **对数**: log, ln
- **聚合**: sum, avg, min, max, count

### 配置
```json
{
  "operation": "add",      // add, subtract, multiply, divide, ...
  "value": 10,             // 第二个操作数
  "roundTo": 2             // 保留小数位
}
```

### 示例

**输入**: `100`
**操作**: multiply by 1.1
**输出**: `110`

## 范围映射节点 (Range)

### 功能
将数值从一个范围映射到另一个范围

### 配置
```json
{
  "minIn": 0,              // 输入最小值
  "maxIn": 100,            // 输入最大值
  "minOut": 0,            // 输出最小值
  "maxOut": 255           // 输出最大值
}
```

### 示例

**输入**: `50` (0-100)
**映射到**: 0-255
**输出**: `127.5`

## 统计分析节点

### 功能
- **Full**: 完整统计 (count, sum, avg, min, max, median, stdDev, variance, range, quartiles)
- **Basic**: 基本统计 (count, sum, avg, min, max)
- **Distribution**: 分布统计 (直方图)

### 配置
```json
{
  "mode": "full",          // full, basic, distribution
  "property": ""           // 要统计的属性 (空表示 payload)
}
```

### 示例输出 (Full)

```json
{
  "count": 100,
  "sum": 5500,
  "avg": 55,
  "min": 10,
  "max": 100,
  "median": 55,
  "stdDev": 26.93,
  "variance": 725.45,
  "range": 90,
  "q1": 32.5,
  "q2": 55,
  "q3": 77.5
}
```

## 批处理节点 (Batch)

### 功能
- **Count**: 按数量分组
- **Time**: 按时间分组
- **Split**: 分割成批次

### 配置
```json
{
  "mode": "count",
  "count": 10,             // 每批数量
  "interval": 1000         // 时间间隔 (毫秒)
}
```

## 限流节点 (Throttle)

### 功能
控制消息发送速率

### 配置
```json
{
  "rate": 5,               // 每秒消息数
  "capacity": 100          // 缓冲区容量
}
```

## 时间戳节点 (Timestamp)

### 功能
- 获取当前时间
- 格式化时间戳
- 时间运算

### 配置
```json
{
  "format": "ISO8601",     // ISO8601, Unix, custom
  "add": 3600              // 增加秒数
}
```

### 输出格式

**ISO8601**: `"2024-01-15T10:30:00Z"`
**Unix**: `1705315800`

## 触发器节点 (Trigger)

### 功能
- 定时触发
- 循环触发
- 可重置

### 配置
```json
{
  "loop": false,
  "interval": 1000,         // 间隔 (毫秒)
  "payload": "triggered"
}
```

## 使用流程示例

### 示例 1: 数据转换流程

```
[Inject] → [JSON Parse] → [Filter] → [CSV Stringify] → [Debug]
```

### 示例 2: 数据分析流程

```
[TCP Input] → [JSON Parse] → [Statistics] → [Dashboard]
```

### 示例 3: 数据清洗流程

```
[HTTP Input] → [JSON Parse] → [Filter] → [Sort] → [JSON Stringify] → [HTTP Response]
```

### 示例 4: 实时计算流程

```
[Inject] → [Calculate (avg)] → [Range] → [Debug]
```

## 性能考虑

### 1. 大数据处理
- 使用 `batch` 节点分批处理
- 避免在循环中处理大量数据

### 2. 正则表达式优化
- 预编译常用正则
- 避免过度使用贪婪匹配

### 3. 内存使用
- 使用 `split` 节点控制内存
- 设置合理的 `limit` 参数

## 错误处理

所有节点都会在错误时返回:
```json
{
  "error": "错误信息"
}
```

## 最佳实践

1. **数据验证**: 在处理前验证数据格式
2. **错误处理**: 使用 `catch` 节点捕获错误
3. **性能监控**: 使用性能监控 API 观察节点性能
4. **日志记录**: 使用 `debug` 节点调试流程
5. **模块化**: 将常用流程封装为子流程

## 总结

现在 Flow Designer 拥有完整的数据处理能力:

- ✅ **15 个数据处理节点**
- ✅ **2 个时间处理节点**
- ✅ **2 个专用服务**
- ✅ **支持 JSON/CSV/XML/YAML**
- ✅ **强大的数学计算**
- ✅ **完整的统计分析**
- ✅ **灵活的字符串操作**

项目现在总共有 **49 个节点**！🎉
