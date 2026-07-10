---
title: "Track and View User Session Data - New Exceptionless Feature!"
---

# Track and View User Session Data - New Exceptionless Feature!

![app user session logging](/assets/img/news/sessions-dashboard-header2-1024x411.png)

To many, this feature may be the missing piece... that connection you've always wanted to make between users, bugs, exceptions, app events, etc. I'm talking about, of course, **user session tracking!**

That's right, you can now use Exceptionless to track users as they use your app, which of course will come in super handy when you want to know exactly what they did to stumble upon or cause an error or trigger an event.

Continue reading to learn more about sessions and how you can enable them for your apps.



## Session Overview

First, you must have a paid (premium) Exceptionless plan to report on sessions if you are hosting with us. This is mainly because of the added resource requirements the feature puts on our infrastructure. We think it's definitely worth it, though!

Sessions can be searched/filtered like all other events - they are just an event type like exceptions or logs.

## What's in a User Session Event?

Each user session records how long they were active and what they did. For instance, the average user session on be.exceptionless.io the first week we monitored it using the feature was two hours.

With that, each user session event has a "Session Events" tab that displays all the relevant events that occurred during that session and allows you to see exactly what that user did. This is especially helpful, of course, if that session lead to an exception or noteworthy event in your app.



  [![App User Session Reporting](/assets/img/news/sessions-event-tab-user-footsteps-300x142.jpg)](/assets/img/news/sessions-event-tab-user-footsteps.jpg)





  All unique data that remains constant throughout the user session is also stored in the event, such as browser and environment information.





  [![app user session unique data](/assets/img/news/sessions-unique-user-data-300x155.jpg)](/assets/img/news/sessions-unique-user-data.jpg)




## Sounds Good. How do I Set it Up?


![sessions-dashboard-nav](/assets/img/news/sessions-dashboard-nav.jpg) First, you'll need to update to the latest client versions to enable sessions, then you'll have to follow the below steps to begin tracking them. Once you've got that set up, visit the new Sessions section under the Reports option on your main dashboard, or navigate directly to https://be.exceptionless.io/session/dashboard. If you are **self hosting**, make sure you [update to Exceptionless 3.2](/new-releases-for-all-the-codes-exceptionless-3-2/) first.

### Turning On Session Tracking

For Exceptionless to track a user for your app, you need to send a user identity with each event. To do so, you need to set the default user identity via the following client methods:

#### C#

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.SetUserIdentity("UNIQUE_ID_OR_EMAIL_ADDRESS", "Display Name");
```

#### JavaScript

```js
exceptionless.ExceptionlessClient.default.config.setUserIdentity('UNIQUE_ID_OR_EMAIL_ADDRESS', 'Display Name');
```

Once the user is set on the config object, it will be applied to all future events.

**Please Note:** In WinForms and WPF applications, a plugin will automatically set the default user to the `**Environment.UserName**` if the default user hasn't been already set. Likewise, if you are in a web environment, we will set the default user to the request principal's identity if the default user hasn't already been set.

If you are using WinForms, WPF, or a Browser App, you can enable sessions by calling the `EnableSessions` extension method.

#### C#

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.UseSessions();
```

#### JavaScript

```js
exceptionless.ExceptionlessClient.default.config.useSessions();
```

## How do Sessions get Created?

Sessions are created in two different ways. Either the client can send a session start event, or we can create it automatically on the server side when an event is processed.

We have a server-side plugin that runs as part of our pipeline process for every event - its sole purpose is to manage sessions by using a hash on the user's identity as a lookup for the session id.

If the session doesnt' exist or the current event is a session event type, a new session id will be created. If we receive a `sessionend` event, we close that session and update the end time on the `sessionstart` event.

We also have a `CloseInactiveSessionsJob` event that runs every few minutes to close sessions that haven't been updated in a set period of time. This allows you to efficiently show who is online and offline during a time window.

## How do I Enable Near Real-Time Online/Offline Then?

We do this by default in our JavaScript, WinForms, and WPF clients when you call the `UseSessions()` method.

In the background, we send a `heartbeat` event every 30 seconds if no other events have been sent in the last 30 seconds.

You can **disable this heartbeat** from being sent by passing `false` as an argument to the `UseSessions()` method.

The WinForms and WPF clients will also send a `SessionEnd` event when the process exits.

## Can I Manually Send SessionStart, SessionEnd, and heartbeat Events?

Sure! You can send these events manually via our client API to start, update, or end a session. Please remember, though, that a user identity must be set.

### C#

```cs
using Exceptionless;
ExceptionlessClient.Default.SubmitSessionStart();
await ExceptionlessClient.Default.SubmitSessionHeartbeatAsync();
await ExceptionlessClient.Default.SubmitSessionEndAsync();
```

### JavaScript

```javascript
exceptionless.ExceptionlessClient.default.submitSessionStart();
exceptionless.ExceptionlessClient.default.submitSessionHeartbeat();
exceptionless.ExceptionlessClient.default.submitSessionEnd();
```

[Source](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.6.4/src/ExceptionlessClient.ts#L112-L128)

## Tell Us What You Think

As always, please send us your feedback. You can post it here in the comments or [submit a GitHub Issue](https://github.com/exceptionless) and we will get back to you as soon as possible! We're always looking for contributors, as well, so don't be afraid to jump in and be the hero the code needs. Contributors get Exceptionless perks!
