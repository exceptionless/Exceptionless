# Serilog.Sinks.File&nbsp;[![Build status](https://github.com/serilog/serilog-sinks-file/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/serilog/serilog-sinks-file/actions)&nbsp;[![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.File.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.File/)&nbsp;[![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki)

Writes [Serilog](https://serilog.net) events to one or more text files.

### Getting started

Install the [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/) package from NuGet:

```powershell
dotnet add package Serilog.Sinks.File
```

To configure the sink in C# code, call `WriteTo.File()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

This will append the time period to the filename, creating a file set like:

```
log20180631.txt
log20180701.txt
log20180702.txt
```

> **Important**: By default, only one process may write to a log file at a given time. See _Shared log files_ below for information on multi-process sharing.

### Limits

To avoid bringing down apps with runaway disk usage the file sink **limits file size to 1GB by default**. Once the limit is reached, no further events will be written until the next roll point (see also: [Rolling policies](#rolling-policies) below).

The limit can be changed or removed using the `fileSizeLimitBytes` parameter.

```csharp
    .WriteTo.File("log.txt", fileSizeLimitBytes: null)
```

For the same reason, only **the most recent 31 files** are retained by default (i.e. one long month). To change or remove this limit, pass the `retainedFileCountLimit` parameter.

```csharp
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: null)
```

### Rolling policies

To create a log file per day or other time period, specify a `rollingInterval` as shown in the examples above.

To roll when the file reaches `fileSizeLimitBytes`, specify `rollOnFileSizeLimit`:

```csharp
    .WriteTo.File("log.txt", rollOnFileSizeLimit: true)
```

This will create a file set like:

```
log.txt
log_001.txt
log_002.txt
```

Specifying both `rollingInterval` and `rollOnFileSizeLimit` will cause both policies to be applied, while specifying neither will result in all events being written to a single file.

Old files will be cleaned up as per `retainedFileCountLimit` - the default is 31.

### XML `<appSettings>` configuration

To use the file sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```powershell
Install-Package Serilog.Settings.AppSettings
```

Instead of configuring the logger in code, call `ReadFrom.AppSettings()`:

```csharp
var log = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

In your application's `App.config` or `Web.config` file, specify the file sink assembly and required path format under the `<appSettings>` node:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:File" value="Serilog.Sinks.File" />
    <add key="serilog:write-to:File.path" value="log.txt" />
```

The parameters that can be set through the `serilog:write-to:File` keys are the method parameters accepted by the `WriteTo.File()` configuration method. This means, for example, that the `fileSizeLimitBytes` parameter can be set with:

```xml
    <add key="serilog:write-to:File.fileSizeLimitBytes" value="1234567" />
```

Omitting the `value` will set the parameter to `null`:

```xml
    <add key="serilog:write-to:File.fileSizeLimitBytes" />
```

In XML and JSON configuration formats, environment variables can be used in setting values. This means, for instance, that the log file path can be based on `TMP` or `APPDATA`:

```xml
    <add key="serilog:write-to:File.path" value="%APPDATA%\MyApp\log.txt" />
```

### JSON `appsettings.json` configuration

To use the file sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```powershell
Install-Package Serilog.Settings.Configuration
```

Instead of configuring the file directly in code, call `ReadFrom.Configuration()`:

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

In your `appsettings.json` file, under the `Serilog` node, :

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "File", "Args": { "path": "log.txt", "rollingInterval": "Day" } }
    ]
  }
}
```

See the XML `<appSettings>` example above for a discussion of available `Args` options.

### Controlling event formatting

The file sink creates events in a fixed text format by default:

```
2018-07-06 09:02:17.148 +10:00 [INF] HTTP GET / responded 200 in 1994 ms
```

The format is controlled using an _output template_, which the file configuration method accepts as an `outputTemplate` parameter.

The default format above corresponds to an output template like:

```csharp
  .WriteTo.File("log.txt",
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
```

##### JSON event formatting

To write events to the file in an alternative format such as [JSON](https://github.com/serilog/serilog-formatting-compact), pass an `ITextFormatter` as the first argument:

```csharp
    // Install-Package Serilog.Formatting.Compact
    .WriteTo.File(new CompactJsonFormatter(), "log.txt")
```

### Shared log files

To enable multi-process shared log files, set `shared` to `true`:

```csharp
    .WriteTo.File("log.txt", shared: true)
```

### Auditing

The file sink can operate as an audit file through `AuditTo`:

```csharp
    .AuditTo.File("audit.txt")
```

Only a limited subset of configuration options are currently available in this mode.

### Performance

By default, the file sink will flush each event written through it to disk. To improve write performance, specifying `buffered: true` will permit the underlying stream to buffer writes.

The [Serilog.Sinks.Async](https://github.com/serilog/serilog-sinks-async) package can be used to wrap the file sink and perform all disk access on a background worker thread.

### Extensibility
[`FileLifecycleHooks`](https://github.com/serilog/serilog-sinks-file/blob/master/src/Serilog.Sinks.File/Sinks/File/FileLifecycleHooks.cs) provide an extensibility point that allows hooking into different parts of the life cycle of a log file.

You can create a hook by extending from [`FileLifecycleHooks`](https://github.com/serilog/serilog-sinks-file/blob/master/src/Serilog.Sinks.File/Sinks/File/FileLifecycleHooks.cs) and overriding the `OnFileOpened` and/or `OnFileDeleting` methods.

- `OnFileOpened` provides access to the underlying stream that log events are written to, before Serilog begins writing events. You can use this to write your own data to the stream (for example, to write a header row), or to wrap the stream in another stream (for example, to add buffering, compression or encryption)

- `OnFileDeleting` provides a means to work with obsolete rolling log files, *before* they are deleted by Serilog's retention mechanism - for example, to archive log files to another location

Available hooks:

- [serilog-sinks-file-header](https://github.com/cocowalla/serilog-sinks-file-header): writes a header to the start of each log file
- [serilog-sinks-file-gzip](https://github.com/cocowalla/serilog-sinks-file-gzip): compresses logs as they are written, using streaming GZIP compression
- [serilog-sinks-file-archive](https://github.com/cocowalla/serilog-sinks-file-archive): archives obsolete rolling log files before they are deleted by Serilog's retention mechanism

_Copyright &copy; 2016 Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
