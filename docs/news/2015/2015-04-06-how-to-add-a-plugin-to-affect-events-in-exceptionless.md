---
title: "How to Add a Plugin to Affect Events in Exceptionless"
---

# How to Add a Plugin to Affect Events in Exceptionless

![Exceptionless Plugins](/assets/img/news/plugins-code-featured1.jpg)A plugin is a client-side addin that is run every time you submit an event.

Plugins can be used to add or remove data from an event, or even allow you to cancel an event submission.

Each client-specific implementation registers a plugin to provide client-specific information like request info, environmental info, etc. These abilities make plugins very powerful.

Let's take a more in-depth look at Exceptionless Plugins and how they are used.



## Pre-Reqs

First, we are assuming that you have already created an account and installed and [configured](/docs/clients/dotnet/configuration) the latest version of the Exceptionless client (plugins require client v3 - released 4/6/2015). If you are still using the 1.x client, you will need to [upgrade](/docs/self-hosting/upgrading-self-hosted-instance) to use plugins.  Please contact support via an in-app support message or our [contact page](/contact/) if you have any questions or need assistance in this area.

## Creating a New Plugin

Before we create our first plugin, it’s important to keep in mind that **each plugin will run every time an event is submitted**. As such, you should ensure your plugins are fast and not super resource-intensive so your app remains as quick as possible.

To create a plugin, you have to specify a System.Action&lt;EventPluginContext&gt;, or create a class that derives from [IEventPlugin](https://github.com/exceptionless/Exceptionless.Net/blob/master/src/Exceptionless/Plugins/IEventPlugin.cs).

Every plugin is passed an [EventPluginContext](https://github.com/exceptionless/Exceptionless.Net/blob/master/src/Exceptionless/Plugins/EventPluginContext.cs), which contains all the valuable contextual information that your plugin may need via the following properties:

* **Client
** The ExceptionlessClient that created the event.
* **Event
** The target event.
* **ContextData
** Allows plugins to access additional contextual data to allow them to add additional data to events.
* **Log
** An ExceptionlessLog implementation that lets you write to the internal logger. This internal logger is used only when debugging the client.
* ****Resolver
**** The ExceptionlessClient\`s dependency resolver. This is useful for resolving other dependencies at runtime that were not requested via constructor injection.

### Exceptionless Plugin Example - Add System Uptime to Feature Usages

The following system uptime plugin derives from IEventPlugin and places the system uptime into every feature usage event as extended data when the plugin’s Run(context) method is called.

```cs
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

**Output in Exceptionless:**

#### [![Exceptionless Plugin System Uptime](/assets/img/news/exceptionless-plugin-system-uptime.png)](/assets/img/news/exceptionless-plugin-system-uptime.png)

_Note:_ We kept the formatting of the uptime simple for the sake of this example, but we recommend using our [open source DateTimeExtensions library](https://github.com/exceptionless/Exceptionless.DateTimeExtensions) if you wish to format it in a really pretty manner.

#### Plugin Priority

You might have noticed that there is a priority attribute with a value of 100. The priority of a plugin determines the order that the plugin will run in (runs in order of lowest to highest, and then by order added). All plugins that ship as part of the client start with a priority of 10 and increment by multiples of 10. If you want your addin to run first, give it a low priority (e.g., 0, 1, 2, 3, 4, 5). If you want it to run last, give it a high priority (>100). By default, if you don’t specify a priority, 0 will be used.

To make sure your plugin runs first (if required), you can inspect the configuration's plugin property in Visual Studio while debugging.

```cs
foreach (var plugin in Exceptionless.ExceptionlessClient.Default.Configuration.Plugins)
    Console.WriteLine(plugin);
```

![Exceptionless Plugin Priority](/assets/img/news/exceptionless-plugin-priority.png)

## Adding the Plugin to Your App

Now that we've created the plugin, we’ll add it when our application starts up by calling one of the Exceptionless.ExceptionlessClient.Default.Configuration.AddPlugin() overloads.

In most cases, we use the following overload to register plugins:

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.AddPlugin<SystemUptimePlugin>();
```

When you add a plugin by specifying the type, we inspect the type and try to find a PriorityAttribute. If we can’t find one, the default value of 0 will be used.

You can also add a plugin by passing a System.Action&lt;EventPluginContext&gt; to AddPlugin.
_Please note that we are specifying a key when adding the action plugin so we can remove it later. If you are not going to be removing your plugin, then you can omit the first argument._

**We pass AddPlugin three arguments:**

* **A unique plugin key** (which can be used to remove the plugin later)
* **Priority**
* **An action** that contains all of our logic to add the system uptime (or whatever your plugin does).

```cs
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

To remove a previously added plugin, you need to call one of the Exceptionless.ExceptionlessClient.Default.Configuration.RemovePlugin overloads.

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.RemovePlugin<SystemUptimePlugin>();
```

If you registered your plugin via an action, you will need to remove the plugin with the key it was added with.

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.RemovePlugin("system-uptime");
```

## How Can You Use Plugins?

Can you think of ways that plugins can help your app? Are you already building some? Let us know what they are and how they help! Eventually, we plan on building a library of useful and common plugins that other developers can easily implement. The more help we've got, the faster that library will grow!
