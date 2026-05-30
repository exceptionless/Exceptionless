# Runtime Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Stable](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/b89c765905dbc6db1421ff0a098127d44665c71f/src/OpenTelemetry.Instrumentation.Runtime/../../README.md#beta) |
| Code Owners | [@twenzel](https://github.com/twenzel), [@xiang17](https://github.com/xiang17) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Runtime)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Runtime)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Runtime)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Runtime)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [.NET Runtime](https://docs.microsoft.com/dotnet) and
collect telemetry about runtime behavior.

## Steps to enable OpenTelemetry.Instrumentation.Runtime

### Step 1: Install package

Add a reference to the
[`OpenTelemetry.Instrumentation.Runtime`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
package.

```shell
dotnet add package OpenTelemetry.Instrumentation.Runtime
```

### Step 2: Enable runtime instrumentation

Runtime instrumentation should be enabled at application startup using the
`AddRuntimeInstrumentation` extension on `MeterProviderBuilder`:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddRuntimeInstrumentation()
    .AddPrometheusHttpListener()
    .Build();
```

Refer to [Program.cs](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/b89c765905dbc6db1421ff0a098127d44665c71f/src/OpenTelemetry.Instrumentation.Runtime/../../examples/runtime-instrumentation/Program.cs) for a
complete demo.

Additionally, the above example snippet sets up the OpenTelemetry Prometheus Exporter
HttpListener as well, which requires adding the package
[`OpenTelemetry.Exporter.Prometheus.HttpListener`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
to the application.

## Metrics

> [!NOTE]
> .NET 9 introduced built-in runtime metrics. As such, when applications target
  .NET 9 or greater this package instead registers a `Meter` to receive the built-in
  `System.Runtime` metrics. See the [.NET Runtime metrics documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime)
  for details of the metric and attribute names for the built-in metrics.

### GC related metrics

#### process.runtime.dotnet.**gc.collections.count**

Number of garbage collections that have occurred since process start.

> [!NOTE]
> .NET uses a [generational GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals#generations)
which divides the heap into different generations numbered 0, 1, and 2. In each
collection the GC decides which generation to search for reclaimable memory,
then it searches that generation and all the lower ones. A GC collection that
searches generations 0, 1, and 2 is called a "gen2" collection, searching
generations 0 and 1 is a "gen1" collection and searching generation 0 only is a
"gen0" collection. The gen0, gen1, and gen2 attribute values for this metric
count respectively the number of gen0, gen1, and gen2 collections which have
occurred since the process started.

| Units           | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| --------------- | ----------------- | ---------- | ---------------- | ---------------- |
| `{collections}` | ObservableCounter | `Int64`    | generation       | gen0, gen1, gen2 |

The metric can be computed using the [GC.CollectionCount](https://docs.microsoft.com/dotnet/api/system.gc.collectioncount)
API:

* `count_gen0_collections = GC.CollectionCount(0) - GC.CollectionCount(1)`
* `count_gen1_collections = GC.CollectionCount(1) - GC.CollectionCount(2)`
* `count_gen2_collections = GC.CollectionCount(2)`

GC.CollectionCount(X) counts the number of times objects in generation X have
been searched during any GC collection. Although it may sound similar, notice
this is not the same as the number of genX collections. For example objects in
generation 0 are searched during gen0, gen1, and gen2 collections so
`GC.CollectionCount(0) = count_gen0_collections + count_gen1_collections + count_gen2_collections`.
This is why the expressions above are not direct assignments.

#### process.runtime.dotnet.**gc.objects.size**

Count of bytes currently in use by objects in the GC heap that haven't been
collected yet.
Fragmentation and other GC committed memory pools are excluded.
The value is available even before first garbage collection has occurred.

| Units   | Instrument Type         | Value Type | Attribute Key(s)  | Attribute Values |
| ------- | ----------------------- | ---------- | ----------------- | ---------------- |
| `bytes` | ObservableUpDownCounter | `Int64`    | No Attributes     | N/A              |

The API used to retrieve the value is:

* [GC.GetTotalMemory](https://docs.microsoft.com/dotnet/api/system.gc.gettotalmemory):
  Retrieves the number of bytes currently thought to be allocated.
The value is an approximate count. API is called with `false`
as a value of forceFullCollection parameter. Returns an instantaneous
value at the time of observation.

#### process.runtime.dotnet.**gc.allocations.size**

Count of bytes allocated on the managed GC heap since the process start.
.NET objects are allocated from this heap. Object allocations from unmanaged languages
such as C/C++ do not use this heap.

> [!NOTE]
> This metric is only available when targeting .NET 6 or later.

| Units   | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| ------- | ----------------- | ---------- | ---------------- | ---------------- |
| `bytes` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [GC.GetTotalAllocatedBytes](https://docs.microsoft.com/dotnet/api/system.gc.gettotalallocatedbytes):
  Gets a count of the bytes allocated over the lifetime of the process. The returned
value does not include any native allocations. The value is an approximate count.

#### process.runtime.dotnet.**gc.committed_memory.size**

The amount of committed virtual memory for the managed GC heap, as
observed during the latest garbage collection. Committed virtual memory may be
larger than the heap size because it includes both memory for storing existing
objects (the heap size) and some extra memory that is ready to handle newly
allocated objects in the future. The value will be unavailable until at least one
garbage collection has occurred.

> [!NOTE]
> This metric is only available when targeting .NET 6 or later.

| Units   | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values |
| ------- | ----------------------- | ---------- | ---------------- | ---------------- |
| `bytes` | ObservableUpDownCounter | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [GCMemoryInfo.TotalCommittedBytes](https://docs.microsoft.com/dotnet/api/system.gcmemoryinfo.totalcommittedbytes):
  Gets the total committed bytes of the managed heap.

#### process.runtime.dotnet.**gc.heap.size**

The heap size (including fragmentation), as observed during the
latest garbage collection. The value will be unavailable until at least one
garbage collection has occurred.

> [!NOTE]
> This metric is only available when targeting .NET 6 or later.

| Units   | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values           |
| ------- | ----------------------- | ---------- | ---------------- | -------------------------- |
| `bytes` | ObservableUpDownCounter | `Int64`    | generation       | gen0, gen1, gen2, loh, poh |

The API used to retrieve the value is:

* [GC.GetGCMemoryInfo().GenerationInfo/[i/].SizeAfterBytes](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo):
  Represents the size in bytes of a generation on exit of the GC reported in GCMemoryInfo.
  Note that this API on .NET 6 has a [bug](https://github.com/dotnet/runtime/pull/60309).
  For .NET 6, heap size is retrieved with an internal method `GC.GetGenerationSize`,
  which is how the [well-known EventCounters](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/available-counters)
  retrieve the values.
  See source code [here](https://github.com/dotnet/runtime/blob/b4dd16b4418de9b3af08ae85f0f3653e55dc420a/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/RuntimeEventSource.cs#L110-L114).

#### process.runtime.dotnet.**gc.heap.fragmentation.size**

The heap fragmentation, as observed during the latest garbage collection.
The value will be unavailable until at least one garbage collection has occurred.

> [!NOTE]
> This metric is only available when targeting .NET 7 or later.

| Units   | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values           |
| ------- | ----------------------- | ---------- | ---------------- | -------------------------- |
| `bytes` | ObservableUpDownCounter | `Int64`    | generation       | gen0, gen1, gen2, loh, poh |

The API used to retrieve the value is:

* [GCGenerationInfo.FragmentationAfterBytes Property](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo.fragmentationafterbytes)
  Gets the fragmentation in bytes on exit from the reported collection.

#### process.runtime.dotnet.**gc.duration**

The total amount of time paused in GC since the process start.

> [!NOTE]
> This metric is only available when targeting .NET 7 or later.

| Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| ----- | ----------------- | ---------- | ---------------- | ---------------- |
| `ns`  | ObservableCounter | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [GC.GetTotalPauseDuration](https://learn.microsoft.com/dotnet/api/system.gc.gettotalpauseduration)
  Gets the total amount of time paused in GC since the beginning of the process.

### JIT Compiler related metrics

These metrics are only available when targeting .NET6 or later.

#### process.runtime.dotnet.**jit.il_compiled.size**

Count of bytes of intermediate language that have been compiled since
the process start.

| Units   | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| ------- | ----------------- | ---------- | ---------------- | ---------------- |
| `bytes` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**jit.methods_compiled.count**

The number of times the JIT compiler compiled a method since the process
start.  The JIT compiler may be invoked multiple times for the same method to compile
with different generic parameters, or because tiered compilation requested different
optimization settings.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| ----------- | ----------------- | ---------- | ---------------- | ---------------- |
| `{methods}` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**jit.compilation_time**

The amount of time the JIT compiler has spent compiling methods since
the process start.

| Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| ----- | ----------------- | ---------- | ---------------- | ---------------- |
| `ns`  | ObservableCounter | `Int64`    | No Attributes    | N/A              |

The APIs used to retrieve the values are:

* [JitInfo.GetCompiledILBytes](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledilbytes):
  Gets the number of bytes of intermediate language that have been compiled.
The scope of this value is global. The same applies for other JIT related metrics.

* [JitInfo.GetCompiledMethodCount](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledmethodcount):
  Gets the number of methods that have been compiled.

* [JitInfo.GetCompilationTime](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompilationtime):
  Gets the amount of time the JIT Compiler has spent compiling methods.

### Threading related metrics

These metrics are only available when targeting .NET 6 or later.

#### process.runtime.dotnet.**monitor.lock_contention.count**

The number of times there was contention when trying to acquire a
monitor lock since the process start. Monitor locks are commonly acquired by using
the lock keyword in C#, or by calling Monitor.Enter() and Monitor.TryEnter().

| Units                      | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| -------------------------- | ----------------- | ---------- | ---------------- | ---------------- |
| `{contended_acquisitions}` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**thread_pool.threads.count**

The number of thread pool threads that currently exist.

| Units       | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values |
| ----------- | ----------------------- | ---------- | ---------------- | ---------------- |
| `{threads}` | ObservableUpDownCounter | `Int32`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**thread_pool.completed_items.count**

The number of work items that have been processed by the thread pool
since the process start.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
| ----------- | ----------------- | ---------- | ---------------- | ---------------- |
| `{items}`   | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**thread_pool.queue.length**

The number of work items that are currently queued to be processed
by the thread pool.

| Units     | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values |
| --------- | ----------------------- | ---------- | ---------------- | ---------------- |
| `{items}` | ObservableUpDownCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**timer.count**

The number of timer instances that are currently active. Timers can
be created by many sources such as System.Threading.Timer, Task.Delay, or the
timeout in a CancellationSource. An active timer is registered to tick at some
point in the future and has not yet been canceled.

| Units      | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values |
| ---------- | ----------------------- | ---------- | ---------------- | ---------------- |
| `{timers}` | ObservableUpDownCounter | `Int64`    | No Attributes    | N/A              |

The APIs used to retrieve the values are:

* [Monitor.LockContentionCount](https://docs.microsoft.com/dotnet/api/system.threading.monitor.lockcontentioncount):
  Gets the number of times there was contention when trying to take the monitor's
  lock.
* [ThreadPool.ThreadCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.threadcount):
  Gets the number of thread pool threads that currently exist.
* [ThreadPool.CompletedWorkItemCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.completedworkitemcount):
  Gets the number of work items that have been processed so far.
* [ThreadPool.PendingWorkItemCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.pendingworkitemcount):
  Gets the number of work items that are currently queued to be processed.
* [Timer.ActiveCount](https://docs.microsoft.com/dotnet/api/system.threading.timer.activecount):
  Gets the number of timers that are currently active. An active timer is registered
  to tick at some point in the future, and has not yet been canceled.

### Assemblies related metrics

#### process.runtime.dotnet.**assemblies.count**

The number of .NET assemblies that are currently loaded.

| Units          | Instrument Type         | Value Type | Attribute Key(s) | Attribute Values |
| -------------- | ----------------------- | ---------- | ---------------- | ---------------- |
| `{assemblies}` | ObservableUpDownCounter | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [AppDomain.GetAssemblies](https://docs.microsoft.com/dotnet/api/system.appdomain.getassemblies):
  Gets the number of the assemblies that have been loaded into the execution context
  of this application domain.

### Exception counter metric

#### process.runtime.dotnet.**exceptions.count**

Count of exceptions that have been thrown in managed code, since the
observation started. The value will be unavailable until an exception has been
thrown after OpenTelemetry.Instrumentation.Runtime initialization.

> [!NOTE]
> The value is tracked by incrementing a counter whenever an AppDomain.FirstChanceException
event occurs. The observation starts when the Runtime instrumentation library is
initialized, so the value will be unavailable until an exception has been
thrown after the initialization.

| Units          | Instrument Type | Value Type | Attribute Key(s) | Attribute Values |
| -------------- | --------------- | ---------- | ---------------- | ---------------- |
| `{exceptions}` | Counter         | `Int64`    | No Attributes    | N/A              |

Relevant API:

* [AppDomain.FirstChanceException](https://docs.microsoft.com/dotnet/api/system.appdomain.firstchanceexception)
  Occurs when an exception is thrown in managed code, before the runtime searches
  the call stack for an exception handler in the application domain.

## Troubleshooting

If a metric is missing, review the [list of metrics](#metrics) to see if the
metric is available in the .NET version you are running.

Some GC related metrics are unavailable until at least one garbage collection
has occurred.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
