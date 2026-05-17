# Flow Designer 构建指南

## 环境要求

### 必需软件
- **.NET SDK 8.0** 或更高版本
- **Node.js 18+** (可选，用于前端资源优化)

### 安装 .NET SDK

#### Windows
```powershell
# 下载并安装 .NET SDK 8.0
# https://dotnet.microsoft.com/download/dotnet/8.0

# 或使用 winget
winget install Microsoft.DotNet.SDK.8
```

#### macOS
```bash
# 使用 Homebrew
brew install --cask dotnet-sdk

# 或直接下载
# https://dotnet.microsoft.com/download/dotnet/8.0
```

#### Linux (Ubuntu/Debian)
```bash
# 添加 Microsoft 包源
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 .NET SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

## 构建步骤

### 1. 验证环境
```bash
# 检查 .NET SDK 版本
dotnet --version
# 应显示: 8.0.xxx

# 检查工作负载
dotnet workload list
```

### 2. 进入项目目录
```bash
cd /workspace/FlowDesigner
```

### 3. 恢复依赖包
```bash
# 恢复所有项目的依赖
dotnet restore

# 如果遇到网络问题，可以使用国内镜像
dotnet restore --source https://nuget.cdn.azure.cn/v3/index.json
```

### 4. 构建项目
```bash
# 构建整个解决方案
dotnet build

# 或分别构建各个项目
dotnet build src/FlowDesigner.Shared/FlowDesigner.Shared.csproj
dotnet build src/FlowDesigner.Api/FlowDesigner.Api.csproj
dotnet build src/FlowDesigner.Web/FlowDesigner.Web.csproj
```

### 5. 运行项目
```bash
# 启动后端 API
cd src/FlowDesigner.Api
dotnet run --urls "http://localhost:5000"

# 新终端 - 启动前端
cd src/FlowDesigner.Web
dotnet run --urls "http://localhost:5001"
```

## 常见构建错误及解决方案

### 错误 1: NuGet 包恢复失败
```
错误: Unable to resolve package
```

**解决方案:**
```bash
# 清理 NuGet 缓存
dotnet nuget locals all --clear

# 重新恢复
dotnet restore --force
```

### 错误 2: 找不到类型或命名空间
```
错误: The type or namespace name 'XXX' could not be found
```

**解决方案:**
1. 检查 using 语句是否正确
2. 检查项目引用是否完整
3. 重新构建 Shared 项目

```bash
dotnet build src/FlowDesigner.Shared/FlowDesigner.Shared.csproj
```

### 错误 3: Blazor 组件编译错误
```
错误: RZ1000 Component 'XXX' not found
```

**解决方案:**
1. 检查 _Imports.razor 文件
2. 确保所有 using 语句正确
3. 清理并重新构建

```bash
dotnet clean
dotnet build
```

### 错误 4: 依赖版本冲突
```
错误: Package 'XXX' is incompatible with 'net8.0'
```

**解决方案:**
```bash
# 更新所有包到最新版本
dotnet list package --outdated
dotnet add package XXX --version latest
```

## 项目特定检查

### 1. DashboardService 依赖检查
确保以下服务已正确注册:
- HighPerformanceExecutionEngine
- PerformanceMonitor

### 2. 模型引用检查
确保 DashboardModels.cs 中的类型可被正确引用:
- SystemStatistics
- FlowRuntimeStatus

### 3. 前端依赖检查
确保 FlowApiService 包含所有必要的 API 方法。

## 构建输出示例

### 成功构建输出
```
Microsoft (R) Build Engine version 17.8.0+b89cb5fde for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  All projects are up-to-date for restore.
  FlowDesigner.Shared -> /workspace/FlowDesigner/src/FlowDesigner.Shared/bin/Debug/net8.0/FlowDesigner.Shared.dll
  FlowDesigner.Api -> /workspace/FlowDesigner/src/FlowDesigner.Api/bin/Debug/net8.0/FlowDesigner.Api.dll
  FlowDesigner.Web -> /workspace/FlowDesigner/src/FlowDesigner.Web/bin/Debug/net8.0/FlowDesigner.Web.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:15.32
```

### 构建失败输出
如果有错误，会显示详细信息:
```
Build FAILED.

/workspace/FlowDesigner/src/FlowDesigner.Api/Services/DashboardService.cs(20,20): error CS0246: The type or namespace name 'XXX' could not be found (are you missing a using directive or an assembly reference?) [/workspace/FlowDesigner/src/FlowDesigner.Api/FlowDesigner.Api.csproj]

    0 Warning(s)
    1 Error(s)
```

## 快速诊断脚本

创建 `build-check.sh` 文件:
```bash
#!/bin/bash

echo "=== Flow Designer 构建检查 ==="
echo ""

echo "1. 检查 .NET SDK 版本..."
dotnet --version
echo ""

echo "2. 清理项目..."
dotnet clean
echo ""

echo "3. 恢复依赖包..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "❌ 依赖恢复失败!"
    exit 1
fi
echo "✅ 依赖恢复成功"
echo ""

echo "4. 构建项目..."
dotnet build
if [ $? -ne 0 ]; then
    echo "❌ 构建失败!"
    exit 1
fi
echo "✅ 构建成功"
echo ""

echo "=== 所有检查通过 ==="
```

运行脚本:
```bash
chmod +x build-check.sh
./build-check.sh
```

## 生产环境构建

### 发布配置
```bash
# 发布 API 项目
dotnet publish src/FlowDesigner.Api/FlowDesigner.Api.csproj \
  -c Release \
  -o publish/api \
  --self-contained false

# 发布 Web 项目
dotnet publish src/FlowDesigner.Web/FlowDesigner.Web.csproj \
  -c Release \
  -o publish/web \
  --self-contained false
```

### Docker 构建
```dockerfile
# Dockerfile 示例
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlowDesigner.Api.dll"]
```

## 性能优化建议

### 1. 启用并行构建
```bash
dotnet build -c Release --parallel
```

### 2. 使用增量构建
```bash
# 第一次构建会慢，后续构建会快很多
dotnet build
```

### 3. 禁用某些功能加快构建
```xml
<!-- 在 .csproj 中 -->
<PropertyGroup>
  <SkipMvcContentGeneration>true</SkipMvcContentGeneration>
</PropertyGroup>
```

## 故障排除清单

- [ ] .NET SDK 8.0 已安装
- [ ] 项目文件完整
- [ ] 依赖包已恢复
- [ ] 没有语法错误
- [ ] 所有引用正确
- [ ] 配置文件正确
- [ ] 端口未被占用

## 获取帮助

如果遇到问题:
1. 查看构建日志
2. 检查错误信息
3. 搜索相关文档
4. 提交 Issue 到项目仓库

---

**提示**: 首次构建可能需要几分钟时间下载依赖包，请耐心等待。
