# Serilog.Formatting.Compact [![Build status](https://ci.appveyor.com/api/projects/status/ch5il2airviylofn?svg=true)](https://ci.appveyor.com/project/serilog/serilog-formatting-compact) [![NuGet](https://img.shields.io/nuget/v/Serilog.Formatting.Compact.svg)](https://nuget.org/packages/Serilog.Formatting.Compact)

A simple, compact JSON-based event format for Serilog. `CompactJsonFormatter` significantly reduces the byte count of small log events when compared with Serilog's default `JsonFormatter`, while remaining human-readable. It achieves this through shorter built-in property names, a leaner format, and by excluding redundant information.

### Sample

A simple `Hello, {User}` event.

```json
{"@t":"2016-06-07T03:44:57.8532799Z","@mt":"Hello, {User}","User":"nblumhardt"}
```

### Getting started

Install from [NuGet](https://nuget.org/packages/Serilog.Formatting.Compact):

```powershell
dotnet add package Serilog.Formatting.Compact
```

The formatter is used in conjunction with sinks that accept `ITextFormatter`. For example, the [file](https://github.com/serilog/serilog-sinks-file) sink:

```csharp
Log.Logger = new LoggerConfiguration()
  .WriteTo.File(new CompactJsonFormatter(), "./logs/myapp.json")
  .CreateLogger();
```
#### XML `<appSettings>` configuration

To specify the formatter in XML `<appSettings>` provide its assembly-qualified type name:

```xml
<appSettings>
  <add key="serilog:using:File" value="Serilog.Sinks.File" />
  <add key="serilog:write-to:File.path" value="./logs/myapp.json" />
  <add key="serilog:write-to:File.formatter"
       value="Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact" />
```
#### JSON `appsettings.json` configuration
To specify formatter in json `appsettings.json` provide its assembly-qualified type name:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "./logs/myapp.json",
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ]
  }
}
```

### Rendered events

`CompactJsonFormatter` will preserve the message template, properties, and formatting information so that the rendered message can be created at a later point. When the
JSON is intended for consumption in an environment without message template rendering, `RenderedCompactJsonFormatter` can be used instead.

Instead of the message template, `RenderedCompactJsonFormatter` writes the fully-rendered message, as well as
an _event id_ [generated from the message template](https://nblumhardt.com/2015/10/assigning-event-types-to-serilog-events/), into the event:

```json
{"@t":"2016-06-07T03:44:57.8532799Z","@m":"Hello, \"nblumhardt\"","@i":"7a8b9c0d","User":"nblumhardt"}
```

### Format details

The format written by the compact formatters is specified generically so that implementations for other logging libraries, including _Microsoft.Extensions.Logging_, are possible if desired.

##### Payload

Each event is a JSON object with event data at the top level. Any JSON property on the payload object is assumed to be a regular property of the event, apart from the reified properties below.

##### Reified properties

The format defines a handful of reified properties that have special meaning:

| Property | Name | Description | Required? |
| -------- | ---- | ----------- | --------- |
| `@t`     | Timestamp | An ISO 8601 timestamp | Yes |
| `@m`     | Message | A fully-rendered message describing the event | |
| `@mt` | Message Template | Alternative to Message; specifies a [message template](http://messagetemplates.org) over the event's properties that provides for rendering into a textual description of the event | |
| `@l` | Level | An implementation-specific level identifier (string or number) | Absence implies "informational"  |
| `@x` | Exception | A language-dependent error representation potentially including backtrace | |
| `@i` | Event id | An implementation specific event id (string or number) | |
| `@r` | Renderings | If `@mt` includes tokens with programming-language-specific formatting, an array of pre-rendered values for each such token | May be omitted; if present, the count of renderings must match the count of formatted tokens exactly |
| `@tr` | Trace id | The id of the trace that was active when the event was created, if any | |
| `@sp` | Span id | The id of the span that was active when the event was created, if any | |

The `@` sigil may be escaped at the start of a user property name by doubling, e.g. `@@name` denotes a property called `@name`.

##### Batch format

When events are batched into a single payload, a newline-delimited stream of JSON documents is required. Either `\n` or `\r\n` delimiters may be used. Batches of newline-separated compact JSON events can use the (unofficial) MIME type `application/vnd.serilog.clef`.

##### Versioning

Versioning would be additive only, with no version identifier; implementations should treat any unrecognised reified properties as if they are user data.

### Comparison

The output and benchmarks below compare the compact JSON formatter with Serilog's built-in `JsonFormatter`.

**Event**

```csharp
Log.Information("Hello, {@User}, {N:x8} at {Now}",
  new
  {
    Name = "nblumhardt",
    Tags = new[] { 1, 2, 3 }
  },
  123,
  DateTime.Now);
```

**Default `JsonFormatter`** 292 bytes

```
{"Timestamp":"2016-06-07T13:44:57.8532799+10:00","Level":"Information","MessageT
emplate":"Hello, {@User}, {N:x8} at {Now}","Properties":{"User":{"Name":"nblumha
rdt","Tags":[1,2,3]},"N":123,"Now":"2016-06-07T13:44:57.8532799+10:00"},"Renderi
ngs":{"N":[{"Format":"x8","Rendering":"0000007b"}]}}
```

**`CompactJsonFormatter`** 187 bytes (0.64)

```
{"@t":"2016-06-07T03:44:57.8532799Z","@mt":"Hello, {@User}, {N:x8} at {Now}","@r
":["0000007b"],"User":{"Name":"nblumhardt","Tags":[1,2,3]},"N":123,"Now":2016-06
-07T13:44:57.8532799+10:00}
```

**Formatting benchmark**

See `test/Serilog.Formatting.Compact.Tests/FormattingBenchmarks.cs`.

|                      Formatter |    Median  |    StdDev | Scaled |
|:------------------------------ |----------: |---------: |------: |
|                `JsonFormatter` | 11.2775 &micro;s | 0.0682 &micro;s |   1.00 |
|         `CompactJsonFormatter` |  6.0315 &micro;s | 0.0429 &micro;s |   0.53 |
|        `JsonFormatter(renderMessage: true)` | 13.7585 &micro;s | 0.1194 &micro;s |   1.22 |
| `RenderedCompactJsonFormatter` |  7.0680 &micro;s | 0.0605 &micro;s |   0.63 |

### Tools

Several tools are available for working with the CLEF format.

 * **[_Analogy.LogViewer.Serilog_](https://github.com/Analogy-LogViewer/Analogy.LogViewer.Serilog)** - CLEF parser for [Analogy Log Viewer](https://github.com/Analogy-LogViewer/Analogy.LogViewer)
 * **[`clef-tool`](https://github.com/datalust/clef-tool)** - a CLI application for processing CLEF files
 * **[Compact Log Format Viewer](https://github.com/warrenbuckley/Compact-Log-Format-Viewer)** - a cross-platform viewer for CLEF files
 * **[`seqcli`](https://github.com/datalust/seqcli)** - pretty-`print` CLEF files at the command-line, or `ingest` CLEF files into [Seq](https://datalust.co/seq) for search, and analysis
 * **[_Serilog.Formatting.Compact.Reader_](https://github.com/serilog/serilog-formatting-compact-reader)** - convert CLEF documents back into Serilog `LogEvent`s

### Customizing output

_Serilog.Formatting.Compact_ is not intended to provide customizable formatters. See [this blog post](https://nblumhardt.com/2021/06/customize-serilog-json-output/) for comprehensive Serilog JSON output customization examples.
