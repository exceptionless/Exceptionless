---
title: "Troubleshooting"
order: 10
---

# Troubleshooting

If your events aren't being sent to the server there are a few things that you can try to diagnose the issue.

## Update Your Client

Please make sure that you are using the [latest version of the client](/docs/clients/dotnet/upgrading).

## Ensure the Queue has Time to Process

If you are using Exceptionless in a scenario where an event is submitted and the process is immediately terminated, then you will need to make sure that the queue is processed before the application ends. Please note that this will happen automatically if your runtime supports it _(portable profiles do not currently support this)_.

Events are queued to disk and sent in the background, if the application isn't running then the events cannot be sent. You can manually force the queue to be processed by calling the following line of code before before the process ends:

```csharp
await ExceptionlessClient.Default.ProcessQueueAsync();
```

This will cause the event queue to be processed synchronously and the events to be reported. If this doesn't solve the issue then please enable client logging and send us the log file.

## How to Locate the Default Isolated Storage Queue Folder

By default, Exceptionless stores errors in an isolated storage folder. You can find this folder using the 1st 8 characters of your API key. So if your API key is `a7aa250fce7e4e36a22a7031cf2337c8`, then you would search in the `C:\ProgramData\IsolatedStorage` folder for a folder named `a7aa250f`.

## Firewall / Proxy

If you are behind a proxy or firewall, please ensure that you can connect to <https://collector.exceptionless.io> and <https://heartbeat.exceptionless.io>.

Your proxy settings should be picked up automatically by the Exceptionless client, but you can also try manually configuring the settings by adding a section to your app/web.config file.

::: info
Some clients may not support proxies. Proxies are not supported in Portable Class Libraries (PCL). If you are only using the `Exceptionless` portable class library package, then proxies will not work.**
:::

```xml
<system.net>
    <defaultProxy useDefaultCredentials="true">
      <proxy proxyaddress="proxyAddress" usesystemdefault="true"/>
    </defaultProxy>
     <!-- Specifying a bypass list may also be required. -->
    <bypasslist>
      <add address="[a-z]+\.exceptionless\.io$" />
    </bypasslist>
</system.net>
```

You also have the option of specifying the proxy in code by setting the `ExceptionlessClient.Configuration.Proxy` property.

## Enable Client Logging

The Exceptionless client can be configured to write diagnostic messages to a log file to help diagnose any issues with the client. You can enable logging via configuration using one of the following methods:

_Make sure you have write access to the file you specify for the log path._

## Configuration File

```csharp
<exceptionless apiKey="YOUR_API_KEY" enableLogging="true" logPath="C:\exceptionless.log" />
```

## Code

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.UseFileLogger("C:\\exceptionless.log");
```

## Debugging Source Code

You can also debug the Exceptionless NuGet packages by configuring the Visual Studio source server integration. Please follow the [Symbol Source documentation](http://tripleemcoder.com/2015/10/04/moving-to-the-new-symbolsource-engine/) on configuring Visual Studio.

---

[Next > Upgrading](/docs/clients/dotnet/upgrading)
