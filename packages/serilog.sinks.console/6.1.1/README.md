# Serilog.Sinks.Console [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.Console.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.Console/) [![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki) [![Help](https://img.shields.io/badge/stackoverflow-serilog-orange.svg)](http://stackoverflow.com/questions/tagged/serilog)

A Serilog sink that writes log events to the Windows Console or an ANSI terminal via standard output. Coloring and custom themes are supported, including ANSI 256-color themes on macOS, Linux and Windows 10+. The default output is plain text; JSON formatting can be plugged in using [_Serilog.Formatting.Compact_](https://github.com/serilog/serilog-formatting-compact) or the [fully-customizable](https://nblumhardt.com/2021/06/customize-serilog-json-output/) [_Serilog.Expressions_](https://github.com/serilog/serilog-expressions).

### Getting started

To use the console sink, first install the [NuGet package](https://nuget.org/packages/serilog.sinks.console):

```shell
dotnet add package Serilog.Sinks.Console
```

Then enable the sink using `WriteTo.Console()`:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Hello, world!");
```

Log events will be printed to `STDOUT`:

```
[12:50:51 INF] Hello, world!
```

### Themes

The sink will colorize output by default:

![Colorized Console](https://raw.githubusercontent.com/serilog/serilog-sinks-console/dev/assets/Screenshot.png)

Themes can be specified when configuring the sink:

```csharp
    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
```

The following built-in themes are available:

 * `ConsoleTheme.None` - no styling
 * `SystemConsoleTheme.Literate` - styled to replicate _Serilog.Sinks.Literate_, using the `System.Console` coloring modes supported on all Windows/.NET targets; **this is the default when no theme is specified**
 * `SystemConsoleTheme.Grayscale` - a theme using only shades of gray, white, and black
 * `AnsiConsoleTheme.Literate` - an ANSI 256-color version of the "literate" theme
 * `AnsiConsoleTheme.Grayscale` - an ANSI 256-color version of the "grayscale" theme
 * `AnsiConsoleTheme.Code` - an ANSI 256-color Visual Studio Code-inspired theme
 * `AnsiConsoleTheme.Sixteen` - an ANSI 16-color theme that works well with both light and dark backgrounds

 Adding a new theme is straightforward; examples can be found in the [`SystemConsoleThemes`](https://github.com/serilog/serilog-sinks-console/blob/dev/src/Serilog.Sinks.Console/Sinks/SystemConsole/Themes/SystemConsoleThemes.cs) and [`AnsiConsoleThemes`](https://github.com/serilog/serilog-sinks-console/blob/dev/src/Serilog.Sinks.Console/Sinks/SystemConsole/Themes/AnsiConsoleThemes.cs) classes.

### Output templates

The format of events to the console can be modified using the `outputTemplate` configuration parameter:

```csharp
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
```

The default template, shown in the example above, uses built-in properties like `Timestamp` and `Level`. Properties from events, including those attached using [enrichers](https://github.com/serilog/serilog/wiki/Enrichment), can also appear in the output template.

### JSON output

The sink can write JSON  output instead of plain text. `CompactJsonFormatter` or `RenderedCompactJsonFormatter` from [Serilog.Formatting.Compact](https://github.com/serilog/serilog-formatting-compact) is recommended:

```shell
dotnet add package Serilog.Formatting.Compact
```

Pass a formatter to the `Console()` configuration method:

```csharp
    .WriteTo.Console(new RenderedCompactJsonFormatter())
```

Output theming is not available when custom formatters are used.

### XML `<appSettings>` configuration

To use the console sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```shell
dotnet add package Serilog.Settings.AppSettings
```

Instead of configuring the logger in code, call `ReadFrom.AppSettings()`:

```csharp
var log = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

In your application's `App.config` or `Web.config` file, specify the console sink assembly under the `<appSettings>` node:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:Console" value="Serilog.Sinks.Console" />
    <add key="serilog:write-to:Console" />
```

To configure the console sink with a different theme and include the `SourceContext` in the output, change your `App.config`/`Web.config` to:
```xml
<configuration>
  <appSettings>
    <add key="serilog:using:Console" value="Serilog.Sinks.Console" />
    <add key="serilog:write-to:Console.theme" value="Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console" />
    <add key="serilog:write-to:Console.outputTemplate" value="[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} &lt;s:{SourceContext}&gt;{NewLine}{Exception}" />
```

### JSON `appsettings.json` configuration

To use the console sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```shell
dotnet add package Serilog.Settings.Configuration
```

Instead of configuring the sink directly in code, call `ReadFrom.Configuration()`:

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
    "WriteTo": [{"Name": "Console"}]
  }
}
```

To configure the console sink with a different theme and include the `SourceContext` in the output, change your `appsettings.json` to:
```json
{
  "Serilog": {
    "WriteTo": [
      {
          "Name": "Console",
          "Args": {
            "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
            "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
          }
      }
    ]
  }
}
```

### Performance

Console logging is synchronous and this can cause bottlenecks in some deployment scenarios. For high-volume console logging, consider using [_Serilog.Sinks.Async_](https://github.com/serilog/serilog-sinks-async) to move console writes to a background thread:

```csharp
// dotnet add package serilog.sinks.async

Log.Logger = new LoggerConfiguration()
    .WriteTo.Async(wt => wt.Console())
    .CreateLogger();
```

### Contributing

Would you like to help make the Serilog console sink even better? We keep a list of issues that are approachable for newcomers under the [up-for-grabs](https://github.com/serilog/serilog-sinks-console/issues?labels=up-for-grabs&state=open) label. Before starting work on a pull request, we suggest commenting on, or raising, an issue on the issue tracker so that we can help and coordinate efforts. For more details check out our [contributing guide](CONTRIBUTING.md).

When contributing please keep in mind our [Code of Conduct](CODE_OF_CONDUCT.md).

_Copyright &copy; Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
