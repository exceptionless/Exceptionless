# Process Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Beta](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/1c377f8b19a6004273c95175da6fc80f1d88c788/src/OpenTelemetry.Instrumentation.Process/../../README.md#beta) |
| Code Owners | [@Yun-Ting](https://github.com/Yun-Ting) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Process)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Process)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Process)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Process)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [.NET](https://docs.microsoft.com/dotnet) and collects
telemetry about process behavior.

The process metric instruments being implemented are following OpenTelemetry
[metrics semantic
conventions](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/system/process-metrics.md).

## Steps to enable OpenTelemetry.Instrumentation.Process

### Step 1: Install package

Add a reference to
[`OpenTelemetry.Instrumentation.Process`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process)
package.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.Process
```

Add a reference to
[`OpenTelemetry.Exporter.Prometheus.HttpListener`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)
package.

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.Prometheus.HttpListener
```

### Step 2: Enable Process instrumentation

Process instrumentation should be enabled at application startup using the
`AddProcessInstrumentation` extension on `MeterProviderBuilder`:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddProcessInstrumentation()
    .AddPrometheusHttpListener()
    .Build();
```

Refer to [Program.cs](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/1c377f8b19a6004273c95175da6fc80f1d88c788/src/OpenTelemetry.Instrumentation.Process/../../examples/process-instrumentation/Program.cs) for a
complete demo. This examples sets up the OpenTelemetry Prometheus exporter,
which requires adding the package
[`OpenTelemetry.Exporter.Prometheus.HttpListener`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
to the application.

Additionally, this
[document](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/getting-started-prometheus-grafana/README.md)
shows how to use Prometheus and Grafana to build a dashboard for your
application.
[This](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/1c377f8b19a6004273c95175da6fc80f1d88c788/src/OpenTelemetry.Instrumentation.Process/../../examples/process-instrumentation/process-instrumentation-grafana-dashboard-sample.json)
is the Grafana dashboard template which has all the metrics currently supported
by this package; plus an additional aggregated metric `CPU utilization`
calculated with the raw metrics, `CPU time` and `CPU count`.

Please follow the instructions in this
[document](https://grafana.com/docs/grafana/v9.0/dashboards/export-import/) to
import a Grafana dashboard by uploading the JSON template file.

## Metrics

### process.memory.usage

The amount of physical memory allocated for this process.

| Units | Instrument Type         | Value Type |
| ----- | ----------------------- | ---------- |
| `By`  | ObservableUpDownCounter | `Double`   |

The API used to retrieve the value is:

* [Process.WorkingSet64](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.workingset64):
Gets the amount of physical memory, in bytes, allocated for the associated
process.

### process.memory.virtual

The amount of committed virtual memory for this process. One way to think of
this is all the address space this process can read from without triggering an
access violation; this includes memory backed solely by RAM, by a
swapfile/pagefile and by other mapped files on disk.

| Units | Instrument Type         | Value Type |
| ----- | ----------------------- | ---------- |
|  `By` | ObservableUpDownCounter | `Double`   |

The API used to retrieve the value is:

* [Process.VirtualMemorySize64](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.virtualmemorysize64):
Gets the amount of the virtual memory, in bytes, allocated for the associated
process.

### process.cpu.time

Total CPU seconds broken down by states.

| Units | Instrument Type   | Value Type | Attribute Key(s)  | Attribute Values |
| ----- | ----------------- | ---------- | ----------------- | ---------------- |
|  `s`  | ObservableCounter | `Double`   | process.cpu.state | user, system     |

The APIs used to retrieve the values are:

* [Process.UserProcessorTime](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.userprocessortime):
Gets the user processor time for this process.

* [Process.PrivilegedProcessorTime](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.privilegedprocessortime):
Gets the privileged processor time for this process.

### process.cpu.count

The number of processors (CPU cores) available to the current process.

| Units         | Instrument Type         | Value Type |
| ------------- | ----------------------- | ---------- |
| `{processors}`| ObservableUpDownCounter | `Int32`    |

The API used to retrieve the value is
[System.Environment.ProcessorCount](https://learn.microsoft.com/dotnet/api/system.environment.processorcount).

> [!NOTE]
> This metric is under
> [discussion](https://github.com/open-telemetry/opentelemetry-specification/issues/3200)
and not part of the [Process Metrics
Spec](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/system/process-metrics.md)
at this time.

### process.thread.count

Process threads count.

| Units      | Instrument Type         | Value Type |
| ---------- | ----------------------- | ---------- |
| `{thread}` | ObservableUpDownCounter | `Int32`    |

The API used to retrieve the value is:

* [Process.Threads](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.threads):
Gets the set of threads that are running in the associated process.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
