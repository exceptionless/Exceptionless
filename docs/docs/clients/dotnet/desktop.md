---
title: "Desktop apps"
description: "Add Exceptionless to WPF and Windows Forms apps on .NET 8+ or .NET Framework 4.6.2+, with an optional crash report dialog, and to .NET MAUI apps."
sources:
  - Exceptionless.Net:src/Platforms/Exceptionless.Wpf/ExceptionlessWpfExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Wpf/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Wpf/Dialogs/CrashReportDialog.xaml
  - Exceptionless.Net:src/Platforms/Exceptionless.Windows/ExceptionlessWindowsExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Windows/Dialogs/CrashReportForm.designer.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Wpf/Exceptionless.Wpf.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Windows/Exceptionless.Windows.csproj
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:samples/Exceptionless.SampleWpf/App.xaml.cs
  - Exceptionless.Net:samples/Exceptionless.SampleWindows/Program.cs
---

# Desktop apps

The `Exceptionless.Wpf` and `Exceptionless.Windows` packages report unhandled exceptions from WPF and Windows Forms
apps, add Windows environment details, and can ask the user to describe what they were doing before the report is sent.
Both target .NET 8 or later on Windows (`net8.0-windows`) and .NET Framework 4.6.2 or later.

| App type      | Package                 |
| ------------- | ----------------------- |
| WPF           | `Exceptionless.Wpf`     |
| Windows Forms | `Exceptionless.Windows` |
| .NET MAUI     | `Exceptionless` (core)  |

## Install

```bash tab="WPF"
dotnet add package Exceptionless.Wpf
```

```bash tab="Windows Forms"
dotnet add package Exceptionless.Windows
```

## Configure

Set your API key, then call `Register()` once when the app starts, before any windows are shown.

The simplest way to set the key is an assembly attribute, which works on .NET 8+ and .NET Framework. Put it in any file
in the app project, such as `AssemblyInfo.cs`:

```csharp
[assembly: Exceptionless.Configuration.Exceptionless("YOUR_API_KEY")]
```

You can also set `ExceptionlessClient.Default.Configuration.ApiKey` in code before `Register()`, or set the
`Exceptionless__ApiKey` environment variable. On .NET Framework, the package can also read the key from `app.config`
(see [Legacy ASP.NET](/docs/clients/dotnet/legacy-aspnet/#the-exceptionless-config-section) for the section format).

### WPF

Call `Register()` from your `Application.Startup` handler in `App.xaml.cs`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleWpf/App.xaml.cs -->

```csharp
using System.Windows;
using Exceptionless;

namespace MyApp {
    public partial class App : Application {
        private void Application_Startup(object sender, StartupEventArgs e) {
            ExceptionlessClient.Default.Register();
        }
    }
}
```

`Register()` hooks `Application.Current.DispatcherUnhandledException`, so call it after the `Application` exists.

### Windows Forms

Call `Register()` at the start of `Main` in `Program.cs`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleWindows/Program.cs -->

```csharp
using System;
using System.Windows.Forms;
using Exceptionless;

namespace MyApp {
    internal static class Program {
        [STAThread]
        private static void Main() {
            ExceptionlessClient.Default.Register();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
```

> [!NOTE]
> If you self-host Exceptionless, add `ServerUrl = "https://exceptionless.example.com"` to the assembly attribute, or
> set `ExceptionlessClient.Default.Configuration.ServerUrl` before `Register()`.

### The crash report dialog

By default, when an unhandled exception occurs, `Register()` shows a dialog that says the app has encountered a problem
and asks for the user's email address and a description of what they were doing. Clicking **Submit Report** sends the
event with that description. Clicking **Cancel** discards it.

To send unhandled exceptions without asking, turn the dialog off:

```csharp
ExceptionlessClient.Default.Register(showDialog: false);
```

### Send events before the app closes

Unhandled exceptions are sent right away, because the app is about to close. Other events are queued and sent in the
background. To send what's left in the queue when the user closes the app, call `UnregisterAsync()`, for example from
WPF's `Application.Exit` handler:

```csharp
private async void Application_Exit(object sender, ExitEventArgs e) {
    await ExceptionlessClient.Default.UnregisterAsync();
}
```

Desktop users often lose their connection. To keep unsent events across restarts, store the queue on disk before calling
`Register()`:

```csharp
ExceptionlessClient.Default.Configuration.UseFolderStorage(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApp", "exceptionless"));
```

## Verify

Add a button that throws, run the app, and click it:

```csharp
private void ThrowButton_Click(object sender, RoutedEventArgs e) {
    throw new InvalidOperationException("Exceptionless test error");
}
```

Click **Submit Report** in the dialog, then open **Stacks** in the app. The error appears within a few seconds.

## What is captured automatically

- **Unhandled exceptions** from `Application.DispatcherUnhandledException` (WPF), `Application.ThreadException` (Windows
  Forms), `AppDomain.UnhandledException`, and unobserved task exceptions.
- **The user's description and email address**, when they fill in the crash report dialog.
- **The Windows user name** as the event's user, unless you set a user identity.
- **Environment details**: machine name, Windows version, .NET version, process, memory, and IP addresses.
- **Your app's version**, read from the entry assembly unless you call `SetVersion`.

Desktop apps often know who the user is. Set it once after sign-in, and turn on sessions to see how people use the app
(see [Sessions](/docs/user-sessions/)):

```csharp
ExceptionlessClient.Default.Configuration.UseSessions();
ExceptionlessClient.Default.Configuration.SetUserIdentity("user-123", "Ada Lovelace");
```

User names, machine names, and IP addresses can be personal data. See
[Private information](/docs/clients/dotnet/private-information/) to leave them out.

## .NET MAUI

Exceptionless.Net 6.2.0 has no MAUI package and no MAUI sample. MAUI apps can use the core `Exceptionless` package,
which targets .NET Standard 2.0. Start the client in `MauiProgram.CreateMauiApp`:

```bash
dotnet add package Exceptionless
```

```csharp
using Exceptionless;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        ExceptionlessClient.Default.Configuration.UseFolderStorage(Path.Combine(FileSystem.AppDataDirectory, "exceptionless"));
        ExceptionlessClient.Default.Startup("YOUR_API_KEY");

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        return builder.Build();
    }
}
```

`Startup()` reports exceptions that reach `AppDomain.UnhandledException` and unobserved task exceptions. There is no
crash report dialog and no MAUI-specific environment information. Submit handled exceptions with
`ex.ToExceptionless().Submit()`, and test on each platform you ship.

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): feature usage, custom data, and user identity.
- [Sessions](/docs/user-sessions/): how sessions and heartbeats work.
- [Configuration](/docs/clients/dotnet/configuration/): queue storage and client logging.

## Ask Exie

```prompt exie
Which versions of my desktop app are reporting the most errors this week?
```

```prompt exie
Show me the latest events for the most frequent crash this week and summarize what users said they were doing.
```

```prompt exie
Mark the stack for this crash as fixed in version 3.2.0.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most common crash in my Exceptionless desktop app project since the last release, read the user descriptions on its events, reproduce it from this code, and fix it.
```
