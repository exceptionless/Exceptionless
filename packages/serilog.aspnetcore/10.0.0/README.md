# Serilog.AspNetCore&nbsp;[![Build status](https://github.com/serilog/serilog-aspnetcore/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/serilog/serilog-aspnetcore/actions)&nbsp;[![NuGet Version](http://img.shields.io/nuget/v/Serilog.AspNetCore.svg?style=flat)](https://www.nuget.org/packages/Serilog.AspNetCore/)

Serilog logging for ASP.NET Core. This package routes ASP.NET Core log messages through Serilog, so you can get information about ASP.NET's internal operations written to the same Serilog sinks as your application events.

With _Serilog.AspNetCore_ installed and configured, you can write log messages directly through Serilog or any `ILogger` interface injected by ASP.NET. All loggers will use the same underlying implementation, levels, and destinations.

**Versioning:** This package tracks the versioning and target framework support of its
[_Microsoft.Extensions.Hosting_](https://nuget.org/packages/Microsoft.Extensions.Hosting) dependency. Most users should choose the version of _Serilog.AspNetCore_ that matches
their application's target framework. I.e. if you're targeting .NET 7.x, choose a 7.x version of _Serilog.AspNetCore_. If
you're targeting .NET 8.x, choose an 8.x _Serilog.AspNetCore_ version, and so on.

### Instructions

**First**, install the _Serilog.AspNetCore_ [NuGet package](https://www.nuget.org/packages/Serilog.AspNetCore) into your app.

```shell
dotnet add package Serilog.AspNetCore
```

**Next**, in your application's _Program.cs_ file, configure Serilog first.  A `try`/`catch` block will ensure any configuration issues are appropriately logged:

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog(); // <-- Add this line
    
    var app = builder.Build();
    app.MapGet("/", () => "Hello World!");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

The `builder.Services.AddSerilog()` call will redirect all log events through your Serilog pipeline.

**Finally**, clean up by removing the remaining configuration for the default logger, including the `"Logging"` section from _appsettings.*.json_ files (this can be replaced with [Serilog configuration](https://github.com/serilog/serilog-settings-configuration) as shown in [the _Sample_ project](https://github.com/serilog/serilog-aspnetcore/blob/dev/samples/Sample/Program.cs), if required).

That's it! With the level bumped up a little you will see log output resembling:

```
[12:01:43 INF] Starting web application
[12:01:44 INF] Now listening on: http://localhost:5000
[12:01:44 INF] Application started. Press Ctrl+C to shut down.
[12:01:44 INF] Hosting environment: Development
[12:01:44 INF] Content root path: serilog-aspnetcore/samples/Sample
[12:01:47 WRN] Failed to determine the https port for redirect.
[12:01:47 INF] Hello, world!
[12:01:47 INF] HTTP GET / responded 200 in 95.0581 ms
```

**Tip:** to see Serilog output in the Visual Studio output window when running under IIS, either select _ASP.NET Core Web Server_ from the _Show output from_ drop-down list, or replace `WriteTo.Console()` in the logger configuration with `WriteTo.Debug()`.

A more complete example, including `appsettings.json` configuration, can be found in [the sample project here](https://github.com/serilog/serilog-aspnetcore/tree/dev/samples/Sample).

### Request logging

The package includes middleware for smarter HTTP request logging. The default request logging implemented by ASP.NET Core is noisy, with multiple events emitted per request. The included middleware condenses these into a single event that carries method, path, status code, and timing information.

As text, this has a format like:

```
[16:05:54 INF] HTTP GET / responded 200 in 227.3253 ms
```

Or [as JSON](https://github.com/serilog/serilog-formatting-compact):

```json
{
  "@t": "2019-06-26T06:05:54.6881162Z",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
  "@r": ["224.5185"],
  "RequestMethod": "GET",
  "RequestPath": "/",
  "StatusCode": 200,
  "Elapsed": 224.5185,
  "RequestId": "0HLNPVG1HI42T:00000001",
  "CorrelationId": null,
  "ConnectionId": "0HLNPVG1HI42T"
}
```

To enable the middleware, first change the minimum level for the noisy ASP.NET Core log sources to `Warning` in your logger configuration or _appsettings.json_ file:

```csharp
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
```

> **Tip:** add `{SourceContext}` to your console logger's output template to see the names of loggers; this can help track down the source of a noisy log event to suppress.

Then, in your application's _Program.cs_, add the middleware with `UseSerilogRequestLogging()`:

```csharp
    var app = builder.Build();

    app.UseSerilogRequestLogging(); // <-- Add this line

    // Other app configuration
```

It's important that the `UseSerilogRequestLogging()` call appears _before_ handlers such as MVC. The middleware will not time or log components that appear before it in the pipeline. (This can be utilized to exclude noisy handlers from logging, such as `UseStaticFiles()`, by placing `UseSerilogRequestLogging()` after them.)

During request processing, additional properties can be attached to the completion event using `IDiagnosticContext.Set()`:

```csharp
    public class HomeController : Controller
    {
        readonly IDiagnosticContext _diagnosticContext;

        public HomeController(IDiagnosticContext diagnosticContext)
        {
            _diagnosticContext = diagnosticContext ??
                throw new ArgumentNullException(nameof(diagnosticContext));
        }

        public IActionResult Index()
        {
            // The request completion event will carry this property
            _diagnosticContext.Set("CatalogLoadTime", 1423);

            return View();
        }
```

This pattern has the advantage of reducing the number of log events that need to be constructed, transmitted, and stored per HTTP request. Having many properties on the same event can also make correlation of request details and other data easier.

The following request information will be added as properties by default:

* `RequestMethod`
* `RequestPath`
* `StatusCode`
* `Elapsed`

You can modify the message template used for request completion events, add additional properties, or change the event level, using the `options` callback on `UseSerilogRequestLogging()`:

```csharp
app.UseSerilogRequestLogging(options =>
{
    // Customize the message template
    options.MessageTemplate = "Handled {RequestPath}";
    
    // Emit debug-level events instead of the defaults
    options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug;
    
    // Attach additional properties to the request completion event
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
    };
});
```

### Two-stage initialization

The example at the top of this page shows how to configure Serilog immediately when the application starts. This has the benefit of catching and reporting exceptions thrown during set-up of the ASP.NET Core host.

The downside of initializing Serilog first is that services from the ASP.NET Core host, including the `appsettings.json` configuration and dependency injection, aren't available yet.

To address this, Serilog supports two-stage initialization. An initial "bootstrap" logger is configured immediately when the program starts, and this is replaced by the fully-configured logger once the host has loaded.

To use this technique, first replace the initial `CreateLogger()` call with `CreateBootstrapLogger()`:

```csharp
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger(); // <-- Change this line!
```

Then, pass a callback to `AddSerilog()` that creates the final logger:

```csharp
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());
```

It's important to note that the final logger **completely replaces** the bootstrap logger: if you want both to log to the console, for instance, you'll need to specify `WriteTo.Console()` in both places, as the example shows.

#### Consuming `appsettings.json` configuration

**Using two-stage initialization**, insert the `ReadFrom.Configuration(builder.Configuration)` call shown in the example above. The JSON configuration syntax is documented in [the _Serilog.Settings.Configuration_ README](https://github.com/serilog/serilog-settings-configuration).

#### Injecting services into enrichers and sinks

**Using two-stage initialization**, insert the `ReadFrom.Services(services)` call shown in the example above. The `ReadFrom.Services()` call will configure the logging pipeline with any registered implementations of the following services:

 * `IDestructuringPolicy`
 * `ILogEventEnricher`
 * `ILogEventFilter`
 * `ILogEventSink`
 * `LoggingLevelSwitch`

### JSON output

The `Console()`, `Debug()`, and `File()` sinks all support JSON-formatted output natively, via the included _Serilog.Formatting.Compact_ package.

To write newline-delimited JSON, pass a `CompactJsonFormatter` or `RenderedCompactJsonFormatter` to the sink configuration method:

```csharp
    .WriteTo.Console(new RenderedCompactJsonFormatter())
```

### Writing to the Azure Diagnostics Log Stream

The Azure Diagnostic Log Stream ships events from any files in the `D:\home\LogFiles\` folder. To enable this for your app, add a file sink to your `LoggerConfiguration`, taking care to set the `shared` and `flushToDiskInterval` parameters:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    // Add this line:
    .WriteTo.File(
       System.IO.Path.Combine(Environment.GetEnvironmentVariable("HOME"), "LogFiles", "Application", "diagnostics.txt"),
       rollingInterval: RollingInterval.Day,
       fileSizeLimitBytes: 10 * 1024 * 1024,
       retainedFileCountLimit: 2,
       rollOnFileSizeLimit: true,
       shared: true,
       flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();
```

### Pushing properties to the `ILogger<T>`

If you want to add extra properties to all log events in a specific part of your code, you can add them to the **`ILogger<T>`** in **Microsoft.Extensions.Logging** with the following code. For this code to work, make sure you have added the `.Enrich.FromLogContext()` to the `.UseSerilog(...)` statement, as specified in the samples above.

```csharp
// Microsoft.Extensions.Logging ILogger<T>
// Yes, it's required to use a dictionary. See https://nblumhardt.com/2016/11/ilogger-beginscope/
using (logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = "svrooij",
    ["OperationType"] = "update",
}))
{
   // UserId and OperationType are set for all logging events in these brackets
}
```

The code above results in the same outcome as if you would push properties in the **LogContext** in Serilog. More details can be found in https://github.com/serilog/serilog/wiki/Enrichment#the-logcontext.

```csharp
// Serilog LogContext
using (LogContext.PushProperty("UserId", "svrooij"))
using (LogContext.PushProperty("OperationType", "update"))
{
    // UserId and OperationType are set for all logging events in these brackets
}
```
