# OpenTelemetry Diagnostics

The `NetCorePal.OpenTelemetry.Diagnostics` package provides OpenTelemetry telemetry integration for the NetCorePal framework, enabling automatic tracing of domain events, integration events, commands, and transaction execution.

## Features

This package provides automatic tracing and telemetry for the following operations:

- **Domain Event Handling**: Tracks the execution of domain event handlers
- **Integration Event Handling**: Tracks the execution of integration event handlers
- **Command Processing**: Tracks the execution of command handlers
- **Transaction Management**: Tracks database transaction begin, commit, and rollback operations

## Installation

Install the package using NuGet package manager:

```bash
dotnet add package NetCorePal.OpenTelemetry.Diagnostics
```

## Configuration

### 1. Basic Configuration

Configure OpenTelemetry in your `Program.cs`:

```csharp
using NetCorePal.OpenTelemetry.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
var otel = builder.Services.AddOpenTelemetry();

// Configure resources
otel.ConfigureResource(resource =>
{
    resource.AddTelemetrySdk();
    resource.AddEnvironmentVariableDetector();
    resource.AddService("YourApplicationName");
});

// Configure tracing
otel.WithTracing(tracing =>
{
    // Add ASP.NET Core instrumentation
    tracing.AddAspNetCoreInstrumentation();
    
    // Add HTTP client instrumentation
    tracing.AddHttpClientInstrumentation();
    
    // Add NetCorePal instrumentation - This is the key!
    tracing.AddNetCorePalInstrumentation();
    
    // Configure sampler
    tracing.SetSampler(new AlwaysOnSampler());
    
    // Add exporter (e.g., OTLP)
    tracing.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://your-otlp-endpoint:4317");
    });
});

var app = builder.Build();
// ... rest of configuration
```

### 2. Using OTLP HTTP Exporter

If you want to export telemetry data using the OTLP HTTP protocol:

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

### 3. Integration with Other Tools

Can be used together with other OpenTelemetry instrumentations:

```csharp
otel.WithTracing(tracing =>
{
    // ASP.NET Core instrumentation
    tracing.AddAspNetCoreInstrumentation();
    
    // HTTP client instrumentation
    tracing.AddHttpClientInstrumentation();
    
    // Entity Framework Core instrumentation
    tracing.AddEntityFrameworkCoreInstrumentation();
    
    // CAP instrumentation (if using DotNetCore.CAP)
    tracing.AddCapInstrumentation();
    
    // MySQL connector
    tracing.AddSource("MySqlConnector");
    
    // NetCorePal instrumentation
    tracing.AddNetCorePalInstrumentation();
});
```

## Traced Activities

### Domain Event Handling

When a domain event is handled, an activity is created with the following information:

- **Activity Name**: `DomainEventHandler: {EventName}`
- **Tags**:
  - `DomainEvent.Id`: Event ID
  - `DomainEvent.Name`: Event name
  - `DomainEvent.EventData`: Event data (JSON serialized)
- **Events**:
  - `DomainEventHandlerBegin`: Handler begins
  - `DomainEventHandlerEnd`: Handler ends
  - If an error occurs, the activity status is set to `Error` and the exception is recorded

### Integration Event Handling

When an integration event is handled, an activity is created with the following information:

- **Activity Name**: `IntegrationEventHandler: {HandlerName}`
- **Tags**:
  - `IntegrationEvent.Id`: Event ID
  - `IntegrationEvent.Name`: Handler name
  - `IntegrationEvent.EventData`: Event data (JSON serialized)
- **Events**:
  - `IntegrationEventHandlerBegin`: Handler begins
  - `IntegrationEventHandlerEnd`: Handler ends
  - If an error occurs, the activity status is set to `Error` and the exception is recorded

### Command Processing

When a command is processed, an activity is created with the following information:

- **Activity Name**: `Command: {CommandName}`
- **Tags**:
  - `CommandBegin.Id`: Command ID
  - `CommandBegin.Name`: Command name
  - `CommandBegin.EventData`: Command data (JSON serialized)
- **Events**:
  - `CommandBegin`: Command begins
  - `CommandEnd`: Command ends
  - If an error occurs, the activity status is set to `Error` and the exception is recorded

### Transaction Management

When a database transaction begins, commits, or rolls back, corresponding activities are created:

- **Activity Name**: `Transaction: {TransactionId}`
- **Tags**:
  - `Transaction.Id`: Transaction ID
- **Events**:
  - `TransactionBegin`: Transaction begins
  - `TransactionCommit`: Transaction commits
  - `TransactionRollback`: Transaction rolls back

## Best Practices

### 1. Use Appropriate Samplers

In production environments, it's recommended to use samplers to control telemetry data volume:

```csharp
// Always sample (development environment)
tracing.SetSampler(new AlwaysOnSampler());

// Sample rate of 10% (production environment)
tracing.SetSampler(new TraceIdRatioBasedSampler(0.1));
```

### 2. Configure Resource Attributes

Add meaningful resource attributes for better service identification:

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

### 3. Export to Multiple Destinations

You can export telemetry data to multiple destinations simultaneously:

```csharp
otel.WithTracing(tracing =>
{
    tracing.AddNetCorePalInstrumentation();
    
    // Export to console (development environment)
    if (builder.Environment.IsDevelopment())
    {
        tracing.AddConsoleExporter();
    }
    
    // Export to OTLP
    tracing.AddOtlpExporter();
    
    // Export to Jaeger
    tracing.AddJaegerExporter();
});
```

## Integration with Observability Platforms

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

## Troubleshooting

### 1. Not Seeing Telemetry Data

Ensure that:
- The `NetCorePal.OpenTelemetry.Diagnostics` package is correctly installed
- `tracing.AddNetCorePalInstrumentation()` has been called
- The exporter is configured correctly
- The sampler is not filtering out all traces

### 2. Activities Not Created

Check:
- Whether NetCorePal framework domain events, integration events, or command handlers are being used
- Whether the diagnostic source subscription is working properly

### 3. Performance Issues

Consider:
- Adjusting the sampling rate
- Avoiding serialization of large objects in event data
- Using batch exporters

## Technical Details

### Diagnostic Source

This package subscribes to the `NetCorePal.Diagnostics` diagnostic source and listens for the following events:

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

### Activity Source

This package uses `NetCorePal.OpenTelemetry` as the activity source name with version `1.0.0`.

## Related Resources

- [OpenTelemetry Official Documentation](https://opentelemetry.io/docs/)
- [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet)
- [NetCorePal Framework Documentation](https://netcorepal.github.io/netcorepal-cloud-framework/)
- [Domain Event Handling](/en/events/domain-event-handler/)
- [Integration Event Handling](/en/events/integration-event-handler/)
