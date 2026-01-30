# OpenTelemetry 诊断

`NetCorePal.OpenTelemetry.Diagnostics` 包提供了对 NetCorePal 框架的 OpenTelemetry 遥测集成，可以自动跟踪领域事件、集成事件、命令和事务的执行情况。

## 功能特性

该包为以下操作提供自动跟踪和遥测：

- **领域事件处理**：跟踪领域事件处理器的执行情况
- **集成事件处理**：跟踪集成事件处理器的执行情况
- **命令处理**：跟踪命令处理器的执行情况
- **事务管理**：跟踪数据库事务的开始、提交和回滚

## 安装

使用 NuGet 包管理器安装包：

```bash
dotnet add package NetCorePal.OpenTelemetry.Diagnostics
```

## 配置

### 1. 基础配置

在 `Program.cs` 中配置 OpenTelemetry：

```csharp
using NetCorePal.OpenTelemetry.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 添加 OpenTelemetry
var otel = builder.Services.AddOpenTelemetry();

// 配置资源
otel.ConfigureResource(resource =>
{
    resource.AddTelemetrySdk();
    resource.AddEnvironmentVariableDetector();
    resource.AddService("YourApplicationName");
});

// 配置跟踪
otel.WithTracing(tracing =>
{
    // 添加 ASP.NET Core 仪器
    tracing.AddAspNetCoreInstrumentation();
    
    // 添加 HTTP 客户端仪器
    tracing.AddHttpClientInstrumentation();
    
    // 添加 NetCorePal 仪器 - 这是关键！
    tracing.AddNetCorePalInstrumentation();
    
    // 配置采样器
    tracing.SetSampler(new AlwaysOnSampler());
    
    // 添加导出器（例如：OTLP）
    tracing.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://your-otlp-endpoint:4317");
    });
});

var app = builder.Build();
// ... 其余配置
```

### 2. 使用 OTLP HTTP 导出器

如果您想使用 OTLP HTTP 协议导出遥测数据：

```csharp
using OpenTelemetry.Exporter;

otel.WithTracing(tracing =>
{
    tracing.AddNetCorePalInstrumentation();
    
    tracing.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://localhost:4318/v1/traces");
        otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
});
```

### 3. 与其他工具集成

可以与其他 OpenTelemetry 仪器一起使用：

```csharp
otel.WithTracing(tracing =>
{
    // ASP.NET Core 仪器
    tracing.AddAspNetCoreInstrumentation();
    
    // HTTP 客户端仪器
    tracing.AddHttpClientInstrumentation();
    
    // Entity Framework Core 仪器
    tracing.AddEntityFrameworkCoreInstrumentation();
    
    // CAP 仪器（如果使用 DotNetCore.CAP）
    tracing.AddCapInstrumentation();
    
    // MySQL 连接器
    tracing.AddSource("MySqlConnector");
    
    // NetCorePal 仪器
    tracing.AddNetCorePalInstrumentation();
});
```

## 跟踪的活动

### 领域事件处理

当领域事件被处理时，会创建一个活动（Activity），包含以下信息：

- **活动名称**：`DomainEventHandler: {EventName}`
- **标签**：
  - `DomainEvent.Id`：事件 ID
  - `DomainEvent.Name`：事件名称
  - `DomainEvent.EventData`：事件数据（JSON 序列化）
- **事件**：
  - `DomainEventHandlerBegin`：处理开始
  - `DomainEventHandlerEnd`：处理结束
  - 如果发生错误，会设置活动状态为 `Error` 并记录异常

### 集成事件处理

当集成事件被处理时，会创建一个活动，包含以下信息：

- **活动名称**：`IntegrationEventHandler: {HandlerName}`
- **标签**：
  - `IntegrationEvent.Id`：事件 ID
  - `IntegrationEvent.Name`：处理器名称
  - `IntegrationEvent.EventData`：事件数据（JSON 序列化）
- **事件**：
  - `IntegrationEventHandlerBegin`：处理开始
  - `IntegrationEventHandlerEnd`：处理结束
  - 如果发生错误，会设置活动状态为 `Error` 并记录异常

### 命令处理

当命令被处理时，会创建一个活动，包含以下信息：

- **活动名称**：`Command: {CommandName}`
- **标签**：
  - `CommandBegin.Id`：命令 ID
  - `CommandBegin.Name`：命令名称
  - `CommandBegin.EventData`：命令数据（JSON 序列化）
- **事件**：
  - `CommandBegin`：命令开始
  - `CommandEnd`：命令结束
  - 如果发生错误，会设置活动状态为 `Error` 并记录异常

### 事务管理

当数据库事务开始、提交或回滚时，会创建相应的活动：

- **活动名称**：`Transaction: {TransactionId}`
- **标签**：
  - `Transaction.Id`：事务 ID
- **事件**：
  - `TransactionBegin`：事务开始
  - `TransactionCommit`：事务提交
  - `TransactionRollback`：事务回滚

## 最佳实践

### 1. 使用适当的采样器

在生产环境中，建议使用采样器来控制遥测数据量：

```csharp
// 始终采样（开发环境）
tracing.SetSampler(new AlwaysOnSampler());

// 采样率为 10%（生产环境）
tracing.SetSampler(new TraceIdRatioBasedSampler(0.1));
```

### 2. 配置资源属性

添加有意义的资源属性以便更好地识别服务：

```csharp
otel.ConfigureResource(resource =>
{
    resource.AddService(
        serviceName: "YourServiceName",
        serviceVersion: "1.0.0",
        serviceInstanceId: Environment.MachineName);
    resource.AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["host.name"] = Environment.MachineName
    });
});
```

### 3. 导出到多个目标

您可以同时导出遥测数据到多个目标：

```csharp
otel.WithTracing(tracing =>
{
    tracing.AddNetCorePalInstrumentation();
    
    // 导出到控制台（开发环境）
    if (builder.Environment.IsDevelopment())
    {
        tracing.AddConsoleExporter();
    }
    
    // 导出到 OTLP
    tracing.AddOtlpExporter();
    
    // 导出到 Jaeger
    tracing.AddJaegerExporter();
});
```

## 与可观测性平台集成

### Jaeger

```csharp
tracing.AddJaegerExporter(options =>
{
    options.AgentHost = "localhost";
    options.AgentPort = 6831;
});
```

### Zipkin

```csharp
tracing.AddZipkinExporter(options =>
{
    options.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
});
```

### Azure Application Insights

```csharp
tracing.AddAzureMonitorTraceExporter(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});
```

## 故障排除

### 1. 没有看到遥测数据

确保：
- 已正确安装 `NetCorePal.OpenTelemetry.Diagnostics` 包
- 已调用 `tracing.AddNetCorePalInstrumentation()`
- 导出器配置正确
- 采样器没有过滤掉所有跟踪

### 2. 活动未创建

检查：
- 是否使用了 NetCorePal 框架的领域事件、集成事件或命令处理器
- 诊断源订阅是否正常工作

### 3. 性能问题

考虑：
- 调整采样率
- 避免在事件数据中序列化大型对象
- 使用批处理导出器

## 技术细节

### 诊断源

该包订阅 `NetCorePal.Diagnostics` 诊断源，并监听以下事件：

- `NetCorePal.DomainEventHandler.Begin`
- `NetCorePal.DomainEventHandler.End`
- `NetCorePal.DomainEventHandler.Error`
- `NetCorePal.IntegrationEventHandler.Begin`
- `NetCorePal.IntegrationEventHandler.End`
- `NetCorePal.IntegrationEventHandler.Error`
- `NetCorePal.CommandHandler.Begin`
- `NetCorePal.CommandHandler.End`
- `NetCorePal.CommandHandler.Error`
- `NetCorePal.Transaction.Begin`
- `NetCorePal.Transaction.Commit`
- `NetCorePal.Transaction.Rollback`

### 活动源

该包使用 `NetCorePal.OpenTelemetry` 作为活动源名称，版本为 `1.0.0`。

## 相关资源

- [OpenTelemetry 官方文档](https://opentelemetry.io/docs/)
- [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet)
- [NetCorePal 框架文档](https://netcorepal.github.io/netcorepal-cloud-framework/)
- [领域事件处理](/zh/events/domain-event-handler/)
- [集成事件处理](/zh/events/integration-event-handler/)
