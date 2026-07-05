---
title: "Configuration"
order: 1
---

# Configuration

- [Installation](#installation)
  - [Browser](#browser)
  - [Node.js](#nodejs)
- [Configuration](#configuration)
  - [Offline Storage](#offline-storage)
  - [API Key](#api-key)
  - [Extended Data](#extended-data)
    - [Default Tags](#default-tags)
    - [Default Data](#default-data)
  - [General Data Protection Regulation](#general-data-protection-regulation)
- [Versioning](#versioning)
- [Self Hosted Options](#self-hosted-options)

***

## Installation

### Browser

1. Install the package by running `npm install @exceptionless/browser`
2. Add the Exceptionless client to your app:

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup((c) => {
  c.apiKey = "API_KEY_HERE";
});
```

### Node.js

1. Install the package by running `npm install @exceptionless/node`.
2. Add the Exceptionless client to your app:

```js
import { Exceptionless } from "@exceptionless/node";

await Exceptionless.startup(c => {
  c.apiKey = "API_KEY_HERE";
});
```

***

## Configuration

_NOTE: The only required setting that you need to configure is the client's `apiKey`._ However, many values may be important for your application. Specifically, you may want to consider persisting events to disk.

### Offline Storage

By default, Exceptionless keeps events in memory and stores server configuration to local storage if available. This means if the application exits before the event can be sent to the server, the event will not be sent on restart. This can be overcome by persisting events to disk as well.

This can be done by setting the configuration value like this:

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.usePersistedQueueStorage = true;
});
```

### API Key

You can set the `apiKey` two different ways. The first is by passing it to the
startup function. This is the recommended way if you have no other client
configuration settings to configure.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup("API_KEY_HERE");
```

The second way is to set it on the configuration instance passed to startup.
This is the recommended way when configuring multiple settings.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.apiKey: 'API_KEY_HERE',
  c.serverUrl: 'http://localhost:5200'
});
```

**NOTE**: creating new instances is good for sending custom events. **Automatic catching of errors uses default client**. Make sure you setup default client as well if you need automatic catching of unhandled errors.

### Extended Data

You can include information that is set globally and provided with every event you send. There are two types of data that can be provided this way: Default Tags and Default Data.

#### Default Tags

To add default tags to every request, you can configure your client like this:

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.defaultTags.push("Tag1", "Tag2");
});
```

#### Default Data

You can set up default data to be sent with every request very similarly to how you send default tags. You would do it like this:

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.defaultData["data"] = "My custom data";
});
```

### General Data Protection Regulation

By default the Exceptionless Client will report all available metadata which could include potential PII data. There are various ways to limit the scope of PII data collection. For example, one could use [Data Exclusions](/docs/security#data-exclusions) to remove sensitive values but it only applies to specific collection points such as `Cookie Keys`, `Form Data Keys`, `Query String Keys` and `Extra Exception properties`. Additional data may need to be removed for the GDPR like the collection of user names and IP Addresses. Shown below is several examples of how you can configure the client to remove this additional metadata.

You have the option of finely tuning what is collected via individual setting options or you can disable the collection of all PII data by setting the `includePrivateInformation` to `false`.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.includePrivateInformation = false;
});
```

If you wish to have a finer grained approach which allows you to use Data Exclusions while removing specific meta data collection you can do so via code. Please note if the below doesn't meet your needs you can always write a plugin.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  // Include the username if available.
  c.includeUserName = false;
  // Include the MachineName in MachineInfo.
  c.includeMachineName = false;
  // Include Ip Addresses in MachineInfo and RequestInfo.
  c.includeIpAddress = false;
  // Include Cookies, please note that DataExclusions are applied to all Cookie keys when enabled.
  c.includeCookies = false;
  // Include Form/POST Data, please note that DataExclusions are only applied to Form data keys when enabled.
  c.includePostData = false;
  // Include Query String information, please note that DataExclusions are applied to all Query String keys when enabled.
  c.includeQueryString = false;
});
```

## Versioning

By specifying an application version you can [enable additional functionality](/docs/versioning). It's a good practice to specify an application version if possible using the code below.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.version = "1.2.3";
});
```

## Self Hosted Options

The Exceptionless client can also be configured to send data to your [self hosted instance](/docs/self-hosting/). This is configured by setting the `serverUrl` setting to point to your Exceptionless instance.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
  c.apiKey: 'API_KEY_HERE',
  c.serverUrl: 'http://localhost:5200'
});
```

***

[Next > Client Configuration Values](/docs/clients/javascript/client-configuration-values)
