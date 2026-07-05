---
title: "Configuration"
order: 1
---

# Configuration

There are a few ways to configure Exceptionless in your project. We'll cover them here or you can jump to app-specific examples: [Console App Example](/docs/clients/dotnet/guides/console-apps-example), [Web Server Example](/docs/clients/dotnet/guides/web-server-example).

---

- [ExceptionlessClient Configuration](#exceptionlessclient-configuration)
  - [Configuring With Code](#configuring-with-code)
  - [Configuring With Attributes](#configuring-with-attributes)
  - [Configuring With Environment Variables](#configuring-with-environment-variables)
  - [Using Web.config](#using-webconfig)
  - [Available Configuration Options](#available-configuration-options)
  - [ServerUrl](#serverurl)
  - [IncludePrivateInformation](#includeprivateinformation)
  - [Extended Data](#extended-data)
  - [Default Tags](#default-tags)
  - [Default Data](#default-data)
- [Versioning](#versioning)
- [Offline storage](#offline-storage)
  - [Configuration File](#configuration-file)
  - [Code](#code)
- [Disabling Exceptionless](#disabling-exceptionless)
  - [Configuration File](#configuration-file-1)
  - [Attribute](#attribute)
- [Self Hosted Options](#self-hosted-options)
  - [Configuration file](#configuration-file-2)
  - [Attribute](#attribute-1)

## ExceptionlessClient Configuration

You have a few options for how you might configure your Exceptionless client. Here are some examples of how to do this.

### Configuring With Code

The examples below show the various ways (configuration file, attributes or code) that Exceptionless can be configured in your application.

```csharp
using Exceptionless;

var client = new ExceptionlessClient(c => {
    c.ApiKey = "YOUR_API_KEY";
    c.SetVersion(version);
});

// You can also set the api key directly on the default instance.
ExceptionlessClient.Default.Configuration.ApiKey = "YOUR_API_KEY"
```

### Configuring With Attributes

You can also configure Exceptionless using attributes like this:

```csharp
using Exceptionless.Configuration;
[assembly: Exceptionless("YOUR_API_KEY")]
```

The Exceptionless assembly attribute will only be picked up if it’s defined in the entry or calling assembly. If you have placed the above attribute in different location you’ll need to call the method below during startup.

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.ReadFromAttributes(typeof(MyClass).Assembly)
```

### Configuring With Environment Variables

You can also add an Environment variable or application setting with the key name `Exceptionless:ApiKey` and your `YOUR_API_KEY` as the value.

### Using Web.config

Exceptionless can be configured using a config section in your web.config or app.config depending on what kind of project you have. Installing the correct NuGet package should automatically add the necessary configuration elements. It should look like this:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="exceptionless" type="Exceptionless.ExceptionlessSection, Exceptionless" />
  </configSections>
  <!-- attribute names are cases sensitive -->
  <exceptionless apiKey="API_KEY_HERE" />
  ...
  <system.webServer>
    <modules>
      <remove name="ExceptionlessModule" />
      <add name="ExceptionlessModule" type="Exceptionless.Mvc.ExceptionlessModule, Exceptionless.Mvc" />
    </modules>
    ...
  </system.webServer>
</configuration>
```

Now, before you can fully configure your Exceptionless client, it's important to know what options are available for you to configure. We'll cover that below.

### Available Configuration Options

When initializing the Exceptionless client, you can set any of the following values:

* ServerUrl
* IncludePrivateInformation
* DefaultTags
* DefaultData

### ServerUrl

The `ServerUrl` is used when you are self-hosting Exceptionless and need to point your client to your self-hosted server. This one is pretty self-explanatory.

### IncludePrivateInformation

This is a boolean value that will automatically strip private info like credit card numbers and passwords from being sent in event handling. The default is `true`. However, you can set this value like this:

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.IncludePrivateInformation = false;
```

You can also set it in a global configuration file like this:

```xml
<exceptionless apiKey="YOUR_API_KEY" includePrivateInformation="false" />
```

### Extended Data

The next two properties that can be set when configuring the Exceptionless client can be considered features that extend your data. If you want to apply additional information to every single event that is fired, you would use one of these two settings.

### Default Tags

Just as you are able to apply tags to individual events, you can set default tags that will apply to all events you submit. Configuring this is simple. Here's a quick example:

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.DefaultTags.Add("Tag1");
```

You can also set this up globally with a configuration file like this:

```xml
<exceptionless apiKey="YOUR_API_KEY" tags="Tag1,Tag2" />
```

### Default Data

When viewing your stacks and individual events, you can see additional information about the events on the Extended Data tab. Data found there is usually passed in by adding info to the data object in the event payload. Here's an example of how you might do that:

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.DefaultData["Data1"] = "Exceptionless";
```

You can also configure this with a configuration file like this:

```xml
<exceptionless apiKey="YOUR_API_KEY">
    <data>
      <add name="Data1" value="Exceptionless"/>
      <add name="Data2" value="10"/>
      <add name="Data3" value="true"/>
      <add name="Data4" value="{ 'Property1': 'Exceptionless', 'Property2: 10, 'Property3': true }"/>
    </data>
</exceptionless>
```

## Versioning

By specifying an application version you can [enable additional functionality](/docs/versioning). By default, an application version will try to be resolved from assembly attributes.  However, it's a good practice to specify an application version if possible using the code below.

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.SetVersion("1.2.3");
```

## Offline storage

By default, Exceptionless keeps events in memory. This means if the application exits before the event can be sent to the server, the event will not be sent on restart. This can be overcome by persisting events to disk.

To persist events to disk for offline scenarios or to ensure no events are lost between application restarts, you will need to configure your Exceptionless client to know to store the events on disk and to know where to store them. You can simply pass in a configuration value that includes the storage path. When selecting a folder path, make sure that the identity the application is running under has full permissions to that folder.

Please note that this adds a bit of overhead as events need to be serialized to disk on submission and is not recommended for high throughput logging scenarios.

### Configuration File

```xml
<!-- Use Folder Storage -->
<exceptionless apiKey="YOUR_API_KEY" storagePath="PATH OR FOLDER NAME" />
```

### Code

```csharp
// Use folder storage
ExceptionlessClient.Default.Configuration.UseFolderStorage("PATH OR FOLDER NAME");
// Use isolated storage
ExceptionlessClient.Default.Configuration.UseIsolatedStorage();
```

## Disabling Exceptionless

You can disable Exceptionless from reporting events during testing using the `Enabled` setting.

### Configuration File

```xml
<exceptionless apiKey="YOUR_API_KEY" enabled="false" />
```

### Attribute

```csharp
using Exceptionless.Configuration;
[assembly: Exceptionless("YOUR_API_KEY", Enabled=false)]
```

## Self Hosted Options

The Exceptionless client can also be configured to send data to your [self hosted instance](/docs/self-hosting/). This is configured by setting the `serverUrl` setting to point to your Exceptionless instance.

### Configuration file

```csharp
<exceptionless apiKey="YOUR_API_KEY" serverUrl="http://localhost" />
```

### Attribute

```csharp
using Exceptionless.Configuration;
[assembly: Exceptionless("YOUR_API_KEY", ServerUrl = "http://localhost")]
```

---
[Next > Client Configuration Values](/docs/clients/dotnet/client-configuration-values)
