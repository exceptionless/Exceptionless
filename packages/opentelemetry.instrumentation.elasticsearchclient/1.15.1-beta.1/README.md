# Elasticsearch Client Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Beta](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/815a68389bb56d0aa88150ba1301a7d301e8e2cd/src/OpenTelemetry.Instrumentation.ElasticsearchClient/../../README.md#beta) |
| Code Owners | [@ejsmith](https://github.com/ejsmith) |

## NEST/Elasticsearch.Net

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.ElasticsearchClient)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.ElasticsearchClient)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.ElasticsearchClient)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.ElasticsearchClient)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [NEST/Elasticsearch.Net](https://www.nuget.org/packages/NEST)
and collects traces about outgoing requests.

> [!NOTE]
> This component is based on the OpenTelemetry semantic conventions for
[metrics](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/db/elasticsearch.md)
and
[traces](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/db/elasticsearch.md).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a
[pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be [breaking changes](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/815a68389bb56d0aa88150ba1301a7d301e8e2cd/src/OpenTelemetry.Instrumentation.ElasticsearchClient/./CHANGELOG.md).

## Steps to enable OpenTelemetry.Instrumentation.ElasticsearchClient

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.ElasticsearchClient`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.ElasticsearchClient
```

### Step 2: Enable NEST/Elasticsearch.Net Instrumentation at application startup

`NEST/Elasticsearch.Net` instrumentation must be enabled at application startup.

The following example demonstrates adding `NEST/Elasticsearch.Net`
instrumentation to a console application. This example also sets up the
OpenTelemetry Console exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddElasticsearchClientInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in the
`ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/815a68389bb56d0aa88150ba1301a7d301e8e2cd/src/OpenTelemetry.Instrumentation.ElasticsearchClient/../OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to the documentation for
[OpenTelemetry.Instrumentation.AspNet](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/815a68389bb56d0aa88150ba1301a7d301e8e2cd/src/OpenTelemetry.Instrumentation.ElasticsearchClient/../OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`ElasticsearchClientInstrumentationOptions`.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddElasticsearchClientInstrumentation(options =>
        {
            // add request json as db.statement attribute tag
            options.SetDbStatementForRequest = true;
        })
        .AddConsoleExporter());
```

When used with
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md),
all configurations to `ElasticsearchClientInstrumentationOptions`
can be done in the `ConfigureServices` method of you applications `Startup`
class as shown below.

```csharp
// Configure
services.Configure<ElasticsearchClientInstrumentationOptions>(options =>
{
    // add request json as db.statement attribute tag
    options.SetDbStatementForRequest = true;
});

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddElasticsearchClientInstrumentation()
        .AddConsoleExporter());
```

## Elastic.Clients.Elasticsearch

[Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch),
that deprecates `NEST/Elasticsearch.Net`,
brings native support for OpenTelemetry. To instrument it you need
to configure the OpenTelemetry SDK to listen to the `ActivitySource`
used by the library by calling `AddSource("Elastic.Clients.Elasticsearch.ElasticsearchClient")`
(Elastic.Clients.Elasticsearch version < 8.10.0) or `AddSource("Elastic.Transport")`
(Elastic.Clients.Elasticsearch version >= 8.10.0)
on the `TracerProviderBuilder`.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Elasticsearch](https://www.elastic.co/)
* [NEST Client](https://www.nuget.org/packages/NEST/)
* [Elasticsearch.Net Client](https://www.nuget.org/packages/Elasticsearch.Net/)
* [Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/)
