# Serilog.Settings.Configuration [![Build status](https://github.com/serilog/serilog-settings-configuration/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/serilog/serilog-settings-configuration/actions)&nbsp;[![NuGet Version](http://img.shields.io/nuget/v/Serilog.Settings.Configuration.svg?style=flat)](https://www.nuget.org/packages/Serilog.Settings.Configuration/)

A Serilog settings provider that reads from [Microsoft.Extensions.Configuration](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-3.1) sources, including .NET Core's `appsettings.json` file.

By default, configuration is read from the `Serilog` section that should be at the **top level** of the configuration file.

```json
{
  "Serilog": {
    "Using":  [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": "Debug",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "Logs/log.txt" } }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ],
    "Destructure": [
      { "Name": "With", "Args": { "policy": "Sample.CustomPolicy, Sample" } },
      { "Name": "ToMaximumDepth", "Args": { "maximumDestructuringDepth": 4 } },
      { "Name": "ToMaximumStringLength", "Args": { "maximumStringLength": 100 } },
      { "Name": "ToMaximumCollectionCount", "Args": { "maximumCollectionCount": 10 } }
    ],
    "Properties": {
        "Application": "Sample"
    }
  }
}
```

After installing this package, use `ReadFrom.Configuration()` and pass an `IConfiguration` object.

```csharp
static void Main(string[] args)
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
        .Build();

    var logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .CreateLogger();

    logger.Information("Hello, world!");
}
```

This example relies on the _[Microsoft.Extensions.Configuration.Json](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json/)_, _[Serilog.Sinks.Console](https://github.com/serilog/serilog-sinks-console)_, _[Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)_, _[Serilog.Enrichers.Environment](https://github.com/serilog/serilog-enrichers-environment)_ and _[Serilog.Enrichers.Thread](https://github.com/serilog/serilog-enrichers-thread)_ packages also being installed.

For a more sophisticated example go to the [sample](sample/Sample) folder.

## Syntax description

### Root section name

Root section name can be changed:

```yaml
{
  "CustomSection": {
    ...
  }
}
```

```csharp
var options = new ConfigurationReaderOptions { SectionName = "CustomSection" };
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration, options)
    .CreateLogger();
```

### Using section and auto-discovery of configuration assemblies

`Using` section contains list of **assemblies** in which configuration methods (`WriteTo.File()`, `Enrich.WithThreadId()`) reside.

```yaml
"Serilog": {
    "Using":  [ "Serilog.Sinks.Console", "Serilog.Enrichers.Thread", /* ... */ ],
    // ...
}
```

For .NET Core projects build tools produce `.deps.json` files and this package implements a convention using `Microsoft.Extensions.DependencyModel` to find any package among dependencies with `Serilog` anywhere in the name and pulls configuration methods from it, so the `Using` section in example above can be omitted:

```yaml
{
  "Serilog": {
    "MinimumLevel": "Debug",
    "WriteTo": [ "Console" ],
    ...
  }
}
```

In order to utilize this convention for .NET Framework projects which are built with .NET Core CLI tools specify `PreserveCompilationContext` to `true` in the csproj properties:

```xml
<PropertyGroup Condition=" '$(TargetFramework)' == 'net46' ">
  <PreserveCompilationContext>true</PreserveCompilationContext>
</PropertyGroup>
```

In case of [non-standard](#azure-functions-v2-v3) dependency management you can pass a custom `DependencyContext` object:

```csharp
var functionDependencyContext = DependencyContext.Load(typeof(Startup).Assembly);

var options = new ConfigurationReaderOptions(functionDependencyContext) { SectionName = "AzureFunctionsJobHost:Serilog" };
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(hostConfig, options)
    .CreateLogger();
```

Alternatively, you can also pass an array of configuration assemblies:

```csharp
var configurationAssemblies = new[]
{
    typeof(ConsoleLoggerConfigurationExtensions).Assembly,
    typeof(FileLoggerConfigurationExtensions).Assembly,
};
var options = new ConfigurationReaderOptions(configurationAssemblies);
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration, options)
    .CreateLogger();
```

For legacy .NET Framework projects it also scans default probing path(s).

For all other cases, as well as in the case of non-conventional configuration assembly names **DO** use [Using](#using-section-and-auto-discovery-of-configuration-assemblies) section.

#### .NET 5.0 onwards Single File Applications

Currently, auto-discovery of configuration assemblies is not supported in bundled mode. **DO** use [Using](#using-section-and-auto-discovery-of-configuration-assemblies) section or explicitly pass a collection of configuration assemblies for workaround.

### MinimumLevel, LevelSwitches, overrides and dynamic reload

The `MinimumLevel` configuration property can be set to a single value as in the sample above, or, levels can be overridden per logging source.

This is useful in ASP.NET Core applications, which will often specify minimum level as:

```json
"MinimumLevel": {
    "Default": "Information",
    "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
    }
}
```

`MinimumLevel` section also respects dynamic reload if the underlying provider supports it.

```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(path: "appsettings.json", reloadOnChange: true)
    .Build();
```

Any changes for `Default`, `Microsoft`, `System` sources will be applied at runtime.

(Note: only existing sources are respected for a dynamic update. Inserting new records in `Override` section is **not** supported.)

You can also declare `LoggingLevelSwitch`-es in custom section and reference them for sink parameters:

```json
{
    "Serilog": {
        "LevelSwitches": { "controlSwitch": "Verbose" },
        "WriteTo": [
            {
                "Name": "Seq",
                "Args": {
                    "serverUrl": "http://localhost:5341",
                    "apiKey": "yeEZyL3SMcxEKUijBjN",
                    "controlLevelSwitch": "$controlSwitch"
                }
            }
        ]
    }
}
```

Level updates to switches are also respected for a dynamic update.

Since version 7.0.0, both declared switches (i.e. `Serilog:LevelSwitches` section) and minimum level override switches (i.e. `Serilog:MinimumLevel:Override` section) are exposed through a callback on the reader options so that a reference can be kept:

```csharp
var allSwitches = new Dictionary<string, LoggingLevelSwitch>();
var options = new ConfigurationReaderOptions
{
    OnLevelSwitchCreated = (switchName, levelSwitch) => allSwitches[switchName] = levelSwitch
};

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration, options)
    .CreateLogger();

LoggingLevelSwitch controlSwitch = allSwitches["$controlSwitch"];
```

### WriteTo, Enrich, AuditTo, Destructure sections

These sections support simplified syntax, for example the following is valid if no arguments are needed by the sinks:

```json
"WriteTo": [ "Console", "DiagnosticTrace" ]
```

Or alternatively, the long-form (`"Name":` ...) syntax from the example above can be used when arguments need to be supplied.

By `Microsoft.Extensions.Configuration.Json` convention, array syntax implicitly defines index for each element in order to make unique paths for configuration keys. So the example above is equivalent to:

```yaml
"WriteTo": {
    "0": "Console",
    "1": "DiagnosticTrace"
}
```

And

```yaml
"WriteTo:0": "Console",
"WriteTo:1": "DiagnosticTrace"
```

(The result paths for the keys will be the same, i.e. `Serilog:WriteTo:0` and `Serilog:WriteTo:1`)

When overriding settings with [environment variables](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-3.1#environment-variables) it becomes less convenient and fragile, so you can specify custom names:

```yaml
"WriteTo": {
    "ConsoleSink": "Console",
    "DiagnosticTraceSink": { "Name": "DiagnosticTrace" }
}
```

### Properties section

This section defines a static list of key-value pairs that will enrich log events.

### Filter section

This section defines filters that will be applied to log events. It is especially useful in combination with _[Serilog.Expressions](https://github.com/serilog/serilog-expressions)_ (or legacy _[Serilog.Filters.Expressions](https://github.com/serilog/serilog-filters-expressions)_) package so you can write expression in text form:

```yaml
"Filter": [{
  "Name": "ByIncludingOnly",
  "Args": {
      "expression": "Application = 'Sample'"
  }
}]
```

Using this package you can also declare `LoggingFilterSwitch`-es in custom section and reference them for filter parameters:

```yaml
{
    "Serilog": {
        "FilterSwitches": { "filterSwitch": "Application = 'Sample'" },
        "Filter": [
            {
                "Name": "ControlledBy",
                "Args": {
                    "switch": "$filterSwitch"
                }
            }
        ]
}
```

Level updates to switches are also respected for a dynamic update.

Since version 7.0.0, filter switches are exposed through a callback on the reader options so that a reference can be kept:

```csharp
var filterSwitches = new Dictionary<string, ILoggingFilterSwitch>();
var options = new ConfigurationReaderOptions
{
    OnFilterSwitchCreated = (switchName, filterSwitch) => filterSwitches[switchName] = filterSwitch
};

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration, options)
    .CreateLogger();

ILoggingFilterSwitch filterSwitch = filterSwitches["filterSwitch"];
```

### Nested configuration sections

Some Serilog packages require a reference to a logger configuration object. The sample program in this project illustrates this with the following entry configuring the _[Serilog.Sinks.Async](https://github.com/serilog/serilog-sinks-async)_ package to wrap the _[Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)_ package. The `configure` parameter references the File sink configuration:

```yaml
"WriteTo:Async": {
  "Name": "Async",
  "Args": {
    "configure": [
      {
        "Name": "File",
        "Args": {
          "path": "%TEMP%/Logs/serilog-configuration-sample.txt",
          "outputTemplate":
              "{Timestamp:o} [{Level:u3}] ({Application}/{MachineName}/{ThreadId}) {Message}{NewLine}{Exception}"
        }
      }
    ]
  }
},
```

### Destructuring

Destructuring means extracting pieces of information from an object and create properties with values; Serilog offers the `@` [structure-capturing operator](https://github.com/serilog/serilog/wiki/Structured-Data#preserving-object-structure). In case there is a need to customize the way log events are serialized (e.g., hide property values or replace them with something else), one can define several destructuring policies, like this:

```yaml
"Destructure": [
  {
    "Name": "With",
    "Args": {
      "policy": "MyFirstNamespace.FirstDestructuringPolicy, MyFirstAssembly"
    }
  },
  {
    "Name": "With",
    "Args": {
      "policy": "MySecondNamespace.SecondDestructuringPolicy, MySecondAssembly"
    }
  },
   {
    "Name": "With",
    "Args": {
      "policy": "MyThirdNamespace.ThirdDestructuringPolicy, MyThirdAssembly"
    }
  },
],
```

This is how the first destructuring policy would look:

```csharp
namespace MyFirstNamespace;

public record MyDto(int Id, int Name);

public class FirstDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value is not MyDto dto)
        {
            result = null;
            return false;
        }

        result = new StructureValue(new List<LogEventProperty>
        {
            new LogEventProperty("Identifier", new ScalarValue(deleteTodoItemInfo.Id)),
            new LogEventProperty("NormalizedName", new ScalarValue(dto.Name.ToUpperInvariant()))
        });

        return true;
    }
}
```

Assuming Serilog needs to destructure an argument of type **MyDto** when handling a log event:

```csharp
logger.Information("About to process input: {@MyDto} ...", myDto);
```

it will apply **FirstDestructuringPolicy** which will convert **MyDto** instance to a **StructureValue** instance; a Serilog console sink would write the following entry:

```text
About to process input: {"Identifier": 191, "NormalizedName": "SOME_UPPER_CASE_NAME"} ...
```

## Arguments binding

When the configuration specifies a discrete value for a parameter (such as a string literal), the package will attempt to convert that value to the target method's declared CLR type of the parameter. Additional explicit handling is provided for parsing strings to `Uri`, `TimeSpan`, `enum`, arrays and custom collections.

Since version 7.0.0, conversion will use the invariant culture (`CultureInfo.InvariantCulture`) as long as the `ReadFrom.Configuration(IConfiguration configuration, ConfigurationReaderOptions options)` method is used. Obsolete methods use the current culture to preserve backward compatibility.

### Static member support

Static member access can be used for passing to the configuration argument via [special](https://github.com/serilog/serilog-settings-configuration/blob/dev/test/Serilog.Settings.Configuration.Tests/StringArgumentValueTests.cs#L35) syntax:

```yaml
{
  "Args": {
     "encoding": "System.Text.Encoding::UTF8",
     "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
  }
}
```

### Complex parameter value binding

If the parameter value is not a discrete value, it will try to find a best matching public constructor for the argument:

```yaml
{
  "Name": "Console",
  "Args": {
    "formatter": {
      // `type` (or $type) is optional, must be specified for abstract declared parameter types
      "type": "Serilog.Templates.ExpressionTemplate, Serilog.Expressions",
      "template": "[{@t:HH:mm:ss} {@l:u3} {Coalesce(SourceContext, '<none>')}] {@m}\n{@x}"
      }
  }
}
```

For other cases the package will use the configuration binding system provided by _[Microsoft.Extensions.Options.ConfigurationExtensions](https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions/)_ to attempt to populate the parameter. Almost anything that can be bound by `IConfiguration.Get<T>` should work with this package. An example of this is the optional `List<Column>` parameter used to configure the .NET Standard version of the _[Serilog.Sinks.MSSqlServer](https://github.com/serilog/serilog-sinks-mssqlserver)_ package.

### Abstract parameter types

If parameter type is an interface or an abstract class you need to specify the full type name that implements abstract type. The implementation type should have parameterless constructor.

```yaml
"Destructure": [
    { "Name": "With", "Args": { "policy": "Sample.CustomPolicy, Sample" } },
    ...
],
```

### IConfiguration parameter

If a Serilog package requires additional external configuration information (for example, access to a `ConnectionStrings` section, which would be outside of the `Serilog` section), the sink should include an `IConfiguration` parameter in the configuration extension method. This package will automatically populate that parameter. It should not be declared in the argument list in the configuration source.

### IConfigurationSection parameters

Certain Serilog packages may require configuration information that can't be easily represented by discrete values or direct binding-friendly representations. An example might be lists of values to remove from a collection of default values. In this case the method can accept an entire `IConfigurationSection` as a call parameter and this package will recognize that and populate the parameter. In this way, Serilog packages can support arbitrarily complex configuration scenarios.

## Samples

### Azure Functions (v2, v3)

hosts.json

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingExcludedTypes": "Request",
      "samplingSettings": {
        "isEnabled": true
      }
    }
  },
  "Serilog": {
    "MinimumLevel": {
        "Default": "Information",
        "Override": {
            "Microsoft": "Warning",
            "System": "Warning"
        }
    },
    "Enrich": [ "FromLogContext" ],
    "WriteTo": [
      { "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }
    ]
  }
}
```

In `Startup.cs` section name should be prefixed with [AzureFunctionsJobHost](https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings#azurefunctionsjobhost__)

```csharp
public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider>(sp =>
        {
            var functionDependencyContext = DependencyContext.Load(typeof(Startup).Assembly);

            var hostConfig = sp.GetRequiredService<IConfiguration>();
            var options = new ConfigurationReaderOptions(functionDependencyContext) { SectionName = "AzureFunctionsJobHost:Serilog" };
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(hostConfig, options)
                .CreateLogger();

            return new SerilogLoggerProvider(logger, dispose: true);
        });
    }
}
```

In order to make auto-discovery of configuration assemblies work, modify Function's csproj file

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- ... -->

  <!-- add this targets -->
  <Target Name="FunctionsPostBuildDepsCopy" AfterTargets="PostBuildEvent">
    <Copy SourceFiles="$(OutDir)$(AssemblyName).deps.json" DestinationFiles="$(OutDir)bin\$(AssemblyName).deps.json" />
  </Target>

  <Target Name="FunctionsPublishDepsCopy" AfterTargets="Publish">
    <Copy SourceFiles="$(OutDir)$(AssemblyName).deps.json" DestinationFiles="$(PublishDir)bin\$(AssemblyName).deps.json" />
  </Target>

</Project>
```

### Versioning

This package tracks the versioning and target framework support of its [_Microsoft.Extensions.Configuration_](https://nuget.org/packages/Microsoft.Extensions.Configuration) dependency.
