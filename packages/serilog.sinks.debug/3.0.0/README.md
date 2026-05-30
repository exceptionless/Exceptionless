# Serilog.Sinks.Debug [![Build status](https://ci.appveyor.com/api/projects/status/oufg4e51868oq4eu?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-debug) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.Debug.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.Debug/) [![Help](https://img.shields.io/badge/stackoverflow-serilog-orange.svg)](http://stackoverflow.com/questions/tagged/serilog)

A Serilog sink that writes log events to the Visual Studio debug output window.

### Getting started

To use the sink, first install the [NuGet package](https://nuget.org/packages/serilog.sinks.debug):

```powershell
dotnet add package Serilog.Sinks.Debug
```

Then enable the sink using `WriteTo.Debug()`:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Debug()
    .CreateLogger();
    
Log.Information("Hello, world!");
```

Log events will be printed to the debug output:

![Debug Output](https://raw.githubusercontent.com/serilog/serilog-sinks-debug/dev/assets/Screenshot.png)

### XML `<appSettings>` configuration

To use the sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```powershell
Install-Package Serilog.Settings.AppSettings
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
    <add key="serilog:using:Debug" value="Serilog.Sinks.Debug" />
    <add key="serilog:write-to:Debug" />
```

### JSON `appsettings.json` configuration

To use the console sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```powershell
Install-Package Serilog.Settings.Configuration
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
    "WriteTo": ["Debug"]
  }
}
```

_Copyright &copy; 2017 Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
