---
title: "Plugins"
order: 8
---

# Plugins

A plugin is a client-side add-in that is run **every time** you submit an event.

- [Create a New Plugin](#create-a-new-plugin)
- [Add System Uptime to Feature Usages](#add-system-uptime-to-feature-usages)
  - [Output](#output)
- [Plugin Priority](#plugin-priority)
- [Adding the Plugin to Your App](#adding-the-plugin-to-your-app)
- [Removing an Existing Plugin](#removing-an-existing-plugin)

## Create a New Plugin

Specify a `System.Action&lt;EventPluginContext&gt;` or create a class that derives from [IEventPlugin](https://github.com/exceptionless/Exceptionless.Net/blob/master/src/Exceptionless/Plugins/IEventPlugin.cs) to create a plugin.

Every plugin is passed an [EventPluginContext](https://github.com/exceptionless/Exceptionless.Net/blob/master/src/Exceptionless/Plugins/EventPluginContext.cs), which contains all the valuable contextual information that your plugin may need via the following properties:

- Client
- Event
- ContextData
- Log
- Resolver

## Add System Uptime to Feature Usages

```csharp
using System;
using System.Diagnostics;
using Exceptionless.Plugins;
using Exceptionless.Models;

namespace Exceptionless.SampleConsole.Plugins {
    [Priority(100)]
    public class SystemUptimePlugin : IEventPlugin {
        public void Run(EventPluginContext context) {
            // Only update feature usage events.
            if (context.Event.Type != Event.KnownTypes.FeatureUsage)
                return;

            // Get the system uptime
            using (var pc = new PerformanceCounter("System", "System Up Time")) {
                pc.NextValue();

                var uptime = TimeSpan.FromSeconds(pc.NextValue());

                // Store the system uptime as an extended property.
                context.Event.SetProperty("System Uptime", String.Format("{0} Days {1} Hours {2} Minutes {3} Seconds", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds));
            }
        }
    }
}
```

### Output

![Exceptionless Plugin Screenshot](/assets/img/news/exceptionless-plugin-system-uptime.png)

## Plugin Priority

The plugin priority determines the order the plugin runs (lowest to highest, then by order added). All plugins shipped with the client have a starting priority of 10 and increment by multiples of 10. For your addin to run first, give it a priority lower than 10 (e.g., 0-5). To have it run last, give it a priority higher than 100. **If a priority is not specified, it defaults to 0.**

## Adding the Plugin to Your App

Start by calling one of the `Exceptionless.ExceptionlessClient.Default.Configuration.AddPlugin()` overloads. This will typically be the following:

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.AddPlugin<SystemUptimePlugin>();
```

Passing a `System.Action&lt;EventPluginContext&gt;` to AddPlugin can also be used to add a plugin. _Note we specify a key so we can remove the plugin later. If you won't be removing the plugin, you can omit the first argument._

**AddPlugin is passed three arguments:**

- Unique Plugin Key (to remove later, if applicable)
- Priority
- Action (logic)

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.AddPlugin("system-uptime", 100, context => {
    // Only update feature usage events.
    if (context.Event.Type != Event.KnownTypes.FeatureUsage)
        return;

    // Get the system uptime
    using (var pc = new PerformanceCounter("System", "System Up Time")) {
         pc.NextValue();
         var uptime = TimeSpan.FromSeconds(pc.NextValue());

         // Store the system uptime as an extended property.
         context.Event.SetProperty("System Uptime", String.Format("{0} Days {1} Hours {2} Minutes {3} Seconds", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds));

     }
});
```

## Removing an Existing Plugin

Call one of the `Exceptionless.ExceptionlessClient.Default.Configuration.RemovePlugin` overloads to remove a plugin.

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.RemovePlugin<SystemUptimePlugin>();
```

If it was registered via an action, you have to remove it via the key you added it with.

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.RemovePlugin("system-uptime");
```

---

[Next > Private Information](/docs/clients/dotnet/private-information)
