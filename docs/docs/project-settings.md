---
title: "Project Settings"
description: "Configure Exceptionless project settings: API keys, data exclusions, error stacking, spam detection, client configuration values, and event exclusions."
---

# Project Settings

Exceptionless is different from most error monitoring services in that it provides you with real-time updates. That includes updates from your Exceptionless project settings to your client. What does that mean?

All project settings are synced to the client in almost real time, so when an event is submitted to Exceptionless we send down a response header with the current configuration version. If a newer version is available we will immediately retrieve and apply the latest configuration. We will also periodically check for newer configuration when the client is idle.

By default the client will check after `5 seconds` on client startup (*if no events are submitted on startup*) and then every `2 minutes` after the last event submission for updated configuration settings.

- Checking for updated settings doesn't count towards plan limits.
- Only the current configuration version is sent when checking for updated settings (no user information will ever be sent).
- If the settings haven't changed, then no settings will be retrieved.

You can also **turn off the automatic updating of configuration settings when idle** in each client respectively. Please see the [client specific documentation](#client-configuration).

Now, let's take a look at the available settings for your projects. There are quite a few, so feel free to jump to the section that interests you below:

<!-- - [Clients](#clients) -->
- [API Keys](#api-keys)
- [Data Exclusions](#data-exclusions)
  - [Example usage](#example-usage)
- [Error Stacking](#error-stacking)
  - [User Namespaces](#user-namespaces)
  - [Common Methods](#common-methods)
- [Spam Detection](#spam-detection)
  - [Automatic Spam Detection](#automatic-spam-detection)
- [Client Configuration](#client-configuration)
- [Event Exclusions](#event-exclusions)
  - [Example](#example)
- [Using Client Configuration Settings](#using-client-configuration-settings)

---

<!-- ## Clients

Exceptionless has a robust API that can be used through any client interface, but as a convenience, we have provided two Exceptionless client wrappers—one for .NET and one for JavaScript.

Each client wrapper has its own guide to help you get started. Visit either one below.

- [.NET](/docs/clients/dotnet/)
- [JavaScript / Node.js](/docs/clients/javascript/) -->

## API Keys

Before doing anything, you want to create an API Key for your project. This protects you and your team by ensuring you are using a revokable key. Instead of using a user-scoped key that would provide access to everything, a project-scoped key is limited to—you guessed it—a single project.

To open your project settings, go to **Settings** > **Projects** in the sidebar and click your project. The settings page has tabs for **General**, **API Keys**, **Settings**, **Configuration**, **Integrations**, **Stack Management**, and **Usage**.

To generate an API Key, click the **API Keys** tab, click **Add token**, and choose **Client API key**. The same menu also creates a **Source map upload token** for uploading source maps from CI/CD.

![API Keys tab with the Add token menu open](/assets/img/docs/project-settings-api-keys-light.webp)

## Data Exclusions

Data can be excluded from your error reports by specifying the information you'd like excluded. This is especially important when you are dealing with sensitive information. For example, an error on log in might, by default, send a user's password to our server. You probably don't want that, so you can exclude that type of data from being sent. For more on how we handle security issues, check out our [security page](/docs/security).

To get started, go to your project settings page and click the **Settings** tab. The **Data Exclusions** section is near the top.

![Data Exclusions section on the project Settings tab](/assets/img/docs/project-settings-data-exclusions-light.webp)

Enter a comma delimited list of field names that should be removed from any error report data (e.g., extended data properties, form fields, cookies and query parameters). You can also specify a field name with wildcards `*` to specify starts with, ends with, or contains just to be extra safe. Changes save automatically when you leave the field.

Turn on the switch below the field to automatically remove user identifiable information from events, such as the machine name, user information, and IP addresses.

### Example usage

- Entering `Password` will remove any field **named** `Password` from the report.
- Entering `Password*` will remove any field that **starts with** `Password` from the report.
- Entering `*Password` will remove any field that **ends with** `Password` from the report.
- Entering `*Password*` will remove any field that **contains** `Password` from the report.

---

## Error Stacking

You can also control how errors are stacked by specifying user namespaces (if you application code utilizes namespaces) or common methods (in all apps) to ignore. Let's take a look at how each works.

### User Namespaces

If your app runs on .NET or other languages that support namespaces, you can define what namespaces your code runs in. This allows Exceptionless to pick a stacking target within your code.

You can set this up on the project settings page in the **Settings** tab, under **Error Stacking** > **User Namespaces**. You simply need to provide a comma delimited list of the namespace names that your applications code belongs to. If this value is set, only methods inside of these namespaces will be considered as stacking targets.

### Common Methods

Slightly different from your namespace definitions above, defining common methods here will *exclude* stacks from building up for specified methods. This is important as you may have catch-all methods in your code or specific methods that you expect to have frequent errors you'd rather not pass to Exceptionless.

You can set this up on the project settings page in the **Settings** tab, under **Error Stacking** > **Common Methods**. Simply supply a comma delimited list of common method names that should not be used as stacking targets.

---

## Spam Detection

Spam detection allows you to prevent noise from being tracked. Bots crawling your site or app can contribute to stacks that you simply do not want to see or track. We make it easy to filter these out.

On your project settings page, click the **Settings** tab and scroll down to the **Spam Detection** section. There, you can specify a comma delimited list of user agents that should be ignored client side. This list supports wildcards and by default covers a vast majority of bots.

![Spam Detection section on the project Settings tab](/assets/img/docs/project-settings-spam-detection-light.webp)

### Automatic Spam Detection

We also provide you with a simple tool to automatically detect spam from a single IP address sending in a high volume of activity. Turn on the switch below the user agent list, and we will automatically hide high volumes of events coming from a single client IP address.

---

## Client Configuration

Custom client configuration values allow you to control the behavior of your app in real time. They are a dictionary of key value pairs (string key, string value). Usage examples include controlling data exclusions to protecting sensitive data and or enabling/disabling features.

To set up custom client configurations, go to **Settings** > **Projects** in the sidebar and click your project. On the project settings page, click the **Configuration** tab. Click the **+** (Add Configuration Value) button, enter a **Key** and **Value**, and click **Add Configuration Value**.

![Configuration tab listing a project's client configuration values](/assets/img/docs/project-settings-configuration-light.webp)

Once you have made changes to your Project Settings, the configuration values will only be read by your client if your client is aware of the configuration. This happens out of the box with the [Exceptionless .NET Client](/docs/clients/dotnet/) and the [Exceptionless JS Client](/docs/clients/javascript/). If you are building your own client or simply wrapping our API, you can still make use of these configuration values by reading them in periodically via a `GET` request to `/api/v2/projects/{id}/config`.

## Event Exclusions

The Exceptionless clients have the ability to automatically discard events based on client configuration settings. We have a plugin that looks at the client configuration settings using a simple key name convention.

To help us understand this consider that every event has a `Type` and `Source` property. The `Type` can be anything but we have a few first class types like [`error`, `usage`, `log`, `404`, `session`](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Models/Event.cs#L92-L100). `Source` is usually set to the exception type when dealing with `error` events or the `log` source.

With that said, you can exclude any event type and source by adding a new client configuration key `@@EVENT_TYPE:SOURCE` by replacing the `EVENT_TYPE` and `SOURCE` respectively and specify a configuration value of `false`. *You can also specify a wild card `*` as part of the `SOURCE`.*

### Example

- To discard all errors of type `System.ArgumentNullException`:
  - **KEY:** `@@error:System.ArgumentNullException`
  - **VALUE:** `false`

- To discard all errors in general you would use a wildcard `*`:
  - **KEY:** `@@error:*`
  - **VALUE:** `false`

- To discard all 404s in general you would use a wildcard `*`:
  - **KEY:** `@@404:*`
  - **VALUE:** `false`

- To discard all 404s for a specific resource, use the resource's event source as the key.
  - **KEY:** `@@404:/invalid-file-path.jpg`
  - **VALUE:** `false`

We have also added some additional known values to support **minimum log levels**. The known values are: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`, `Off`.

- Sets a minimum log level of `Info` for my application:
  - **KEY:** `@@log:*`
  - **VALUE:** `Info`

- You can also set a log level on a per log source basis. This will override any general minimum log level (e.g., `@@log:*` you have defined!
  - **KEY:** `@@log:*AuthController`
  - **VALUE:** `Trace`

## Using Client Configuration Settings

- [.NET](/docs/clients/dotnet/client-configuration-values)
- [JavaScript / Node.js](/docs/clients/javascript/client-configuration-values)

---

[Next > Versioning](/docs/versioning)
