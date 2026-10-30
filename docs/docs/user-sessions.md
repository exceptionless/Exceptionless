---
title: "Sessions"
metaTitle: "Track user sessions in Exceptionless"
description: "See what a user did before an error: how Exceptionless groups events into sessions, how clients start, heartbeat, and end them, and the Sessions view."
sources:
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/70_SessionPlugin.cs
  - src/Exceptionless.Core/Jobs/CloseInactiveSessionsJob.cs
  - src/Exceptionless.Core/Extensions/PersistentEventExtensions.cs
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/sessions/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/sessions/components/session-table-columns.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/sessions/components/sessions-stats-dashboard.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/events-overview.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/session-events.svelte
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/100_HeartbeatPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/016_SetEnvironmentUserPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/900_CancelSessionsWithNoUserPlugin.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessAspNetCorePlugin.cs
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/HeartbeatPlugin.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserLifeCyclePlugin.ts
  - Exceptionless.JavaScript:packages/core/test/DocumentationExamples.test.ts
---

# Sessions

A session is one user's visit to your app: every error, log, feature usage, and 404 they triggered, in order. Use
sessions to see what a user did before an error, or how people use your app. Sessions are on paid plans.

## How sessions work

Every event that has a **user identity** belongs to a session. Exceptionless groups a user's events on the server, so
you get sessions as soon as your events carry a user identity:

- The first event from a user starts a session. Exceptionless creates a session start event for it, with the user, the
  version, the location, and the environment information from that first event.
- Later events from the same user, in the same project and environment, join that session.
- A session ends when the client sends a session end, or when the user has had no events and no heartbeats for five
  minutes.
- The session's duration runs from its first event to its last activity.

Events without a user identity or a session ID don't belong to a session.

A client can also manage sessions itself:

- **Session start**, **heartbeat**, and **session end** events mark when a session begins, show that the user is still
  active while nothing else is being sent, and close the session.
- A client can give events its own session ID instead of relying on the user identity. Session IDs are 8 to 100 letters,
  digits, or dashes.

## Set the user identity

Set the user identity once your app knows who the user is, and every event after that carries it. Use a stable, unique
value such as a user ID or email address, and optionally a display name.

Some clients set it for you:

- In ASP.NET Core, the .NET client uses the signed-in user's name when you haven't set an identity.
- In WinForms and WPF apps, the .NET client uses the Windows user name.

To set it in code, see [sending events in .NET](/docs/clients/dotnet/sending-events/) and
[sending events in JavaScript](/docs/clients/javascript/sending-events/).

> [!NOTE]
> If the project removes user identifiable information (see [data exclusions](/docs/project-settings/#data-exclusions)),
> events still group into sessions, but the user identity isn't stored on them.

## Turn on client sessions

In apps with one user at a time, such as browser, desktop, and mobile apps, turn on sessions in the client. The client
then:

- sends a session start when it starts up,
- sends a heartbeat every minute while the app stays open, counting from the last event, so an idle user's session stays
  open,
- sends a session end when the app shuts down. In the browser, that's when the page unloads.

<!-- source: Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs (SetUserIdentity, UseSessions) and Exceptionless.JavaScript:packages/core/test/DocumentationExamples.test.ts -->

```csharp tab=".NET"
using Exceptionless;

var client = ExceptionlessClient.Default;
client.Configuration.SetUserIdentity("user-123", "Jane Doe");
client.Configuration.UseSessions();
client.Startup("YOUR_API_KEY");
```

```ts tab="JavaScript"
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.setUserIdentity("user-123", "Jane Doe")
  c.useSessions()
})
```

Both clients accept options on `UseSessions()` and `useSessions()`: whether to send heartbeats, the heartbeat interval
(one minute by default, at least 30 seconds), and whether the client generates its own session ID.

Don't turn on client sessions in a server app that handles many users. Set the user identity on each event instead, as
ASP.NET Core does, and Exceptionless groups each user's events into sessions on the server.

You can also send session start, heartbeat, and end events yourself, for example when a user signs in or out. The client
pages show how:

- [.NET: sending events](/docs/clients/dotnet/sending-events/)
- [JavaScript: sending events](/docs/clients/javascript/sending-events/)
- [Client specification](/docs/reference/client-spec/) for the heartbeat endpoint that custom clients call

## View sessions

Click **Sessions** in the sidebar to see every session in the date range.

![Sessions view with totals, a chart, and the session list](/assets/img/docs/user-sessions-dashboard-light.webp)

- The cards show the **Total** number of sessions, the average sessions per hour (**Avg/hr**), the number of **Users**,
  and the average **Duration**.
- The chart shows sessions over time. Drag across it to zoom into a time range.
- The list shows each session's **Summary**, **Duration**, **User**, and **Date**. The **Environment** column is hidden
  by default; add it with **View** > **Manage Columns...**.
- Turn on **View Active** to show only sessions that haven't ended.
- Filter by project, date, environment, version, tag, and more with the filter bar. See
  [Filtering and searching](/docs/filtering-and-searching/).
- Save a filtered list as a view with **View**. See [Saved views](/docs/saved-views/).

Click a session to open its session start event.

## See the events in a session

Any event that belongs to a session has a **Session Events** tab in its details. It lists every event in the session
with its **Summary**, **User**, and **Session Time**. On a session start event, the tab also shows the session's
**Duration** and the user.

Click an event in the list to open it. Click **Open events filtered to this session** (the button above the list) to see
the session's events in the **Events** view, where you can filter and sort them.

## Ask Exie

[Exie](/docs/ai/exie/) is on Medium plans and up.

```prompt exie
How many sessions started in production over the last 7 days?
```

```prompt exie
Which users had the most sessions this week?
```

```prompt exie
What errors did jane@example.com run into today?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can follow a user's session while it debugs:

```prompt agent
Find the latest error for user jane@example.com in Exceptionless, list her events from the hour before it in order, and tell me what she did before it happened.
```
