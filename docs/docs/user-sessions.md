---
title: "User Sessions"
description: "Track user sessions in Exceptionless from .NET and JavaScript: set a user identity, enable sessions, send start, heartbeat, and end events, and view reports."
---

# User Sessions

With user session tracking, you can easily see what a user is doing that leads up to an event occurrence, or just see how they are using your app.

Each session has a list of events (feature usages, exceptions, log messages, etc.) that the user triggered. Open any event in the session and click the **Session Events** tab to see them. Click an event to drill down, or use the events button above the list to open the Events view filtered to that session.

![Session Events tab on an event](/assets/img/docs/user-sessions-session-events-light.webp)

Browser and environment information, along with any other data that persists throughout the user session, is stored as well.

Once you set up session tracking, click **Sessions** in the sidebar to see every session in the selected time range, with totals for sessions, users, and average duration. Turn on **View Active** to show only sessions that have not ended. Click a session to open its details. You can also click the session ID on any event's **Overview** tab.

![Sessions dashboard](/assets/img/docs/user-sessions-dashboard-light.webp)

## Turn On Session Tracking

Set a default user identity via the following client methods to send the user ID for each event. Once set, it will be applied for all future events.

### C# Set User Identity Example

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.SetUserIdentity("UNIQUE_ID_OR_EMAIL_ADDRESS", "Display Name");
```

### JavaScript Set User Identity Example

```javascript
import { Exceptionless } from "@exceptionless/browser";

Exceptionless.config.setUserIdentity("UNIQUE_ID_OR_EMAIL_ADDRESS", "Display Name");
```

**Please Note: In WinForms and WPF applications**, a plugin will automatically set the default user to the `Environment.UserName` if the default user hasn’t been already set. Likewise, if you are in a web environment, we will set the default user to the request principal’s identity if the default user hasn’t already been set.

**If you are using WinForms, WPF, or a Browser App**, you can enable sessions by calling the `UseSessions` extension method.

### C# Use Sessions Example

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.UseSessions();
```

### JavaScript Use Sessions Example

```javascript
import { Exceptionless } from "@exceptionless/browser";

Exceptionless.config.useSessions();
```

## Manually Send SessionStart, SessionEnd, and heartbeat Events

You can use our client API to start, update, or end a session. Just remember, a user identity must be set.

### C# Submit Session Events Example

```csharp
using Exceptionless;
ExceptionlessClient.Default.SubmitSessionStart();
await ExceptionlessClient.Default.SubmitSessionHeartbeatAsync();
await ExceptionlessClient.Default.SubmitSessionEndAsync();
```

### JavaScript Submit Session Events Example

```javascript
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.submitSessionStart();
await Exceptionless.submitSessionHeartbeat();
await Exceptionless.submitSessionEnd();
```

## Disable Heartbeat

If you would like to disable the near real-time session tracking heartbeat that goes out ever 30 seconds, you can pass `false` as an argument to the `UseSessions()` method.

---

[Next > Notifications](/docs/notifications)
