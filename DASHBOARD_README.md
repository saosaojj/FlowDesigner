# 工业大屏系统 (Industrial Dashboard)

## 概述

新增了功能完整的工业大屏系统，支持数据可视化、设备监控、报警管理等功能。

## 功能特性

### 1. 大屏展示
- **实时数据更新** - 支持2秒自动刷新
- **多种组件类型**
  - 数字卡片 (Number Card) - 显示关键指标
  - 状态指示器 (Status Indicator) - 显示运行状态
  - 仪表盘 (Gauge) - 显示百分比指标
  - 进度条 (Progress Bar) - 显示资源使用率
  - 图表 (Chart) - 显示趋势数据
  - 设备列表 (Device List) - 监控设备状态
  - 报警列表 (Alarm List) - 显示最近报警

### 2. 数据模型
- `DashboardConfig` - 大屏配置
- `DashboardWidget` - 组件配置
- `DashboardDataSnapshot` - 数据快照
- `DeviceStatus` - 设备状态
- `AlarmRecord` - 报警记录

### 3. API 接口
```
GET    /api/dashboards                    - 获取所有大屏配置
GET    /api/dashboards/{id}               - 获取指定大屏配置
GET    /api/dashboards/{id}/data          - 获取大屏数据
POST   /api/dashboards                    - 创建大屏配置
PUT    /api/dashboards/{id}               - 更新大屏配置
DELETE /api/dashboards/{id}               - 删除大屏配置
GET    /api/dashboards/templates/list     - 获取模板列表
GET    /api/dashboards/alarms/recent      - 获取最近报警
POST   /api/dashboards/alarms             - 添加报警
POST   /api/dashboards/alarms/{id}/ack    - 确认报警
GET    /api/dashboards/devices/list       - 获取设备列表
PUT    /api/dashboards/devices/{id}       - 更新设备状态
```

## 文件结构

### 新增文件
```
src/FlowDesigner.Shared/
└── Models/
    └── DashboardModels.cs        # 大屏数据模型

src/FlowDesigner.Api/
├── Services/
│   └── DashboardService.cs      # 大屏数据服务
└── Controllers/
    └── DashboardsController.cs  # 大屏 API 控制器 (在 NodesController 中)

src/FlowDesigner.Web/
├── Pages/
│   └── Index.razor              # 大屏首页 (同时也是 Dashboard)
├── Services/
│   └── FlowApiService.cs        # 新增 Dashboard API 方法
├── MainLayout.razor             # 更新 - 新增导航栏
└── wwwroot/css/
    └── app.css                  # 更新 - 新增大屏样式
```

### 更新文件
1. `src/FlowDesigner.Api/Program.cs` - 注册 DashboardService
2. `src/FlowDesigner.Web/MainLayout.razor` - 添加导航栏
3. `src/FlowDesigner.Web/Pages/Index.razor` - 实现大屏页面
4. `src/FlowDesigner.Web/Services/FlowApiService.cs` - 添加 API 方法
5. `src/FlowDesigner.Web/wwwroot/css/app.css` - 添加大屏样式

## 使用说明

### 启动项目
```bash
# 进入项目目录
cd /workspace/FlowDesigner

# 启动后端 API
cd src/FlowDesigner.Api
dotnet run --urls "http://localhost:5000"

# 启动前端 (新终端)
cd ../FlowDesigner.Web
dotnet run --urls "http://localhost:5001"
```

### 访问大屏
- 首页 (同时也是 Dashboard): http://localhost:5001
- 直接访问: http://localhost:5001/dashboard
- Swagger API: http://localhost:5000/swagger

### 预设大屏模板
1. **工业生产总览** (`default-dashboard`)
   - 流程状态监控
   - 活跃流程数
   - 执行统计
   - 系统资源监控
   - 设备状态
   - 实时数据趋势
   - 报警列表

2. **PLC 监控** (`plc-dashboard`)
   - PLC 连接状态
   - Modbus 状态
   - S7 状态
   - 实时数据点

## 界面预览

### 顶部导航栏
- **Flows** - 流程列表 (暂时隐藏)
- **Dashboard** - 大屏展示 (当前)

### 大屏组件
- **第一行**: 流程状态、活跃流程、执行次数、成功率
- **第二行**: CPU、内存、队列使用率
- **第三、四行**: 设备列表、实时趋势、报警列表

### 样式特点
- 深色工业风主题
- 渐变背景
- 毛玻璃效果
- 响应式布局
- 悬停动画

## 扩展开发

### 添加新组件类型
1. 在 `DashboardWidgetType` 中新增枚举
2. 在 `DashboardService.GenerateWidgetData` 中添加处理
3. 在前端添加对应渲染逻辑

### 自定义数据源
1. 在 `WidgetDataConfig` 中配置数据源
2. 在 `DashboardService` 中实现数据获取
3. 可对接真实的 PLC、传感器等数据源

### 添加新大屏
```csharp
var dashboard = new DashboardConfig
{
    Name = "My Custom Dashboard",
    RefreshInterval = 2000,
    Widgets = new List<DashboardWidget>
    {
        // 添加组件
    }
};
```

## 注意事项

1. 目前使用模拟数据，生产环境请连接真实数据源
2. 当前将 Dashboard 和 FlowList 合并在 Index 页面
3. 后续可分离为独立页面
4. DashboardService 中包含默认的模拟设备和报警数据

## 下一步计划

- [ ] 添加大屏编辑功能 (拖拽式)
- [ ] 实现更多图表类型 (饼图、柱状图)
- [ ] 添加大屏数据导出功能
- [ ] 支持多标签页切换不同大屏
- [ ] 添加用户权限控制
- [ ] 连接真实的 PLC 和传感器数据
