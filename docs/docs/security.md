---
title: "Security and privacy"
description: "How Exceptionless handles your event data: encryption in transit, private information clients collect and how to turn it off, data exclusions, and API keys."
navTitle: "Security and privacy"
sources:
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/90_RemovePrivateInformationPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/40_RequestInfoPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/45_EnvironmentInfoPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/EventContext.cs
  - src/Exceptionless.Core/Extensions/RequestInfoExtensions.cs
  - src/Exceptionless.Core/Extensions/StringExtensions.cs
  - src/Exceptionless.Core/Authorization/AuthorizationRoles.cs
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Endpoints/ProjectEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/OrganizationHandler.cs
  - src/Exceptionless.Web/Assistant/AssistantService.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/settings/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/api-keys/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(auth)/login/+page.svelte
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/RequestInfoCollector.cs
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/Utils.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeRequestInfoPlugin.ts
---

# Security and privacy

Error reports can contain personal and sensitive data: user names, IP addresses, cookies, form fields. This page
explains what the clients collect, how to keep sensitive data out of Exceptionless, and who can see your data and send
events to your projects.

For what Exie and the MCP server can see and do, see [AI privacy and security](/docs/ai/privacy/). For the hosted
service's privacy policy, see [Privacy](/privacy/).

## Encryption in transit

The clients send events over HTTPS by default, and the hosted app and API at `be.exceptionless.io` and
`api.exceptionless.io` use HTTPS. On a self-hosted instance, you provide the TLS certificate; see
[Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/).

## What the clients collect

Besides the error, log message, or other event you send, the clients add context that helps you debug it:

- **Environment**: the OS, runtime, process, and machine name, and the machine's IP address.
- **Request** (web apps): the URL, HTTP method, user agent, referrer, client IP address, headers, cookies, query string,
  and form or POST data. In ASP.NET Core, POST data is collected only for unhandled errors.
- **User**: the user identity you set in your app. In WinForms and WPF apps, the .NET client uses the signed-in OS user
  name when you don't set one.

The ASP.NET Core and Node.js clients never send the `Authorization` or `Proxy-Authorization` request headers.

### Turn off private information

The clients treat the user name, machine name, IP addresses, headers, cookies, POST data, and query string as private
information. One setting turns all of them off:

```json tab=".NET"
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY",
    "IncludePrivateInformation": false
  }
}
```

```ts tab="JavaScript"
import { Exceptionless } from "@exceptionless/browser" // or "@exceptionless/node"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.includePrivateInformation = false
})
```

Each kind also has its own setting, such as `IncludeCookies` in .NET and `includeCookies` in JavaScript, so you can keep
some and drop others. See [.NET private information](/docs/clients/dotnet/private-information/) and
[JavaScript configuration](/docs/clients/javascript/client-configuration/).

### Remove private information on the server

To strip private information from every event a project receives, without changing your apps, open the project's
**Settings** page and turn on **Automatically remove user identifiable information from events**. Before an event is
stored, the server then removes:

- the user identity, and the email address from a user description sent with the event (a description added later
  through the user description API keeps its email address)
- IP addresses, from both the request and the environment
- the machine name
- cookies, the query string, and POST data

Request headers are not removed by this setting; turn them off in the client.

When a client leaves IP addresses out, the server fills in the IP address that submitted the event. Turn on this setting
to keep IP addresses out entirely. It also turns off the project's
[bot protection](/docs/concepts/limits/#bots-and-spam), which works by IP address.

## Data exclusions {#data-exclusions}

Data exclusions remove fields by name: extended data properties, form fields, cookies, query string parameters, and
headers. Set them on the project's **Settings** page under **Data Exclusions**, as a comma-delimited list.

![The Data Exclusions setting on a project's Settings page](/assets/img/docs/project-settings-data-exclusions-light.webp)

Names are matched without regard to case, and `*` matches any text at the start or end:

| Exclusion    | Removes fields that                            |
| ------------ | ---------------------------------------------- |
| `Password`   | are named `Password`                           |
| `Password*`  | start with `Password`, such as `PasswordHash`  |
| `*Password`  | end with `Password`, such as `ConfirmPassword` |
| `*Password*` | contain `Password` anywhere                    |

For example: `*Password*, CreditCard*, SSN`.

The clients download the list with the project's
[client configuration values](/docs/project-settings/#client-configuration) and remove matching fields before the event
leaves your app. The server applies the list again to cookies, the query string, and POST data before storing an event,
which covers clients that haven't picked up a change yet.

You can also add exclusions in code, so they apply from the first event: `AddDataExclusions` in .NET and
`addDataExclusions` in JavaScript.

## Who can see your data

Events belong to a project, and projects belong to an organization. Every user in an organization can see and manage all
of its projects, events, settings, users, and billing. There are no per-project or read-only roles, so use separate
organizations for people who shouldn't see each other's data.

To take away someone's access, open the organization's **Users** page and choose **Remove User**. See
[Organizations and billing](/docs/organizations/).

Users sign in with an email address and password, or with GitHub, Google, or Microsoft. Passwords are stored as salted
hashes. Self-hosted instances choose which sign-in options to offer; see
[Configuration](/docs/self-hosting/configuration/).

## API keys

Projects send events with a client API key, which you manage on the project's **API Keys** page. A client API key can
only:

- submit events, user descriptions, and session heartbeats to its project
- read the project's client configuration values
- connect the project to Zapier, which then receives the project's new events

It can't read events or stacks through the API or change project settings. That makes it fit to ship in browser, mobile,
and desktop apps, but anyone who has the key can do the things above. Don't store secrets in client configuration
values.

A **Source map upload token**, added on the same page, is for uploading source maps to its project from CI.

If a key leaks, add a new key with **Add token**, deploy it, and then choose **Disable Token** or **Delete** on the old
key from its row menu. You can enable a disabled key again later.

To read data from the API, use a user token or OAuth instead; see [Authentication](/docs/api/authentication/).

## Self-hosting

A self-hosted instance keeps all event data in your own Elasticsearch, Redis, and storage. If you turn on Exie, your
questions and the data its tools return go to the AI provider you configure; see [AI features](/docs/self-hosting/ai/).
See [Self-hosting](/docs/self-hosting/).
