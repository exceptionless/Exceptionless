---
title: "Feature usage"
description: "Count how often each feature of your app is used, and by how many users, with Exceptionless feature usage events and the Most Used Features view."
sources:
  - src/Exceptionless.Core/Pipeline/010_AssignToStackAction.cs
  - src/Exceptionless.Core/Plugins/Formatting/Default/40_UsageFormattingPlugin.cs
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/options.ts
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Models/Collections/SettingsDictionary.cs
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
---

# Feature usage

Feature usage events count how often the features of your app are used and by how many users. Send one each time someone
uses a feature you care about, such as exporting a report or starting a checkout, and Exceptionless ranks your features
by use.

## Send feature usage

A feature usage event has the type `usage`, and its source is the feature's name:

```csharp tab=".NET"
using Exceptionless;

ExceptionlessClient.Default.SubmitFeatureUsage("Export Report");
```

```ts tab="JavaScript"
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.submitFeatureUsage("Export Report")
```

```bash tab="curl"
curl -X POST "https://api.exceptionless.io/api/v2/events" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{ "type": "usage", "source": "Export Report" }'
```

To attach the user, tags, or custom data, build the event with `CreateFeatureUsage` (.NET) or `createFeatureUsage`
(JavaScript) and submit it. See the sending events page for [.NET](/docs/clients/dotnet/sending-events/) or
[JavaScript](/docs/clients/javascript/sending-events/).

Use the same name every time for the same feature. Each feature name becomes one stack, so `Export Report` and
`export-report` are counted as two features.

## See which features are used most

Open **Stacks** > **Most Used Features**. It lists one stack per feature for the last 7 days, with the most used first.
The **Events** column is how many times the feature was used, and **Users** is the share of users who used it. Change
the **Date** filter to look at another period.

Select a feature's stack to see its usage over the last 7 days, and step through the individual events to see who used
it and when.

To list the individual events instead, add a **Type** filter set to **Feature Usage** on an **Events** view, or use
`type:usage` in a raw filter. Save either as a [saved view](/docs/saved-views/) to keep it.

> [!TIP]
> **Most Used Features**, like the other built-in views, shows only open and regressed stacks. Don't mark feature usage
> stacks fixed, ignored, or snoozed if you want to keep counting them there.

## Stop counting a feature

To stop collecting a feature's events, open its stack and choose **Discarded**. New events for it are dropped and don't
count against your plan.

The .NET and JavaScript clients can also skip a feature before sending it. Add the client configuration value `@@usage:`
followed by the feature name, such as `@@usage:Export Report`, with the value `false`, on the project's
**Configuration** tab. A `*` wildcard works too, as in `@@usage:Admin*`. See
[Projects](/docs/project-settings/#client-configuration).

## Ask Exie

```prompt exie
Which features were used most in the last 30 days?
```

```prompt exie
How many times was Export Report used each day this month?
```

```prompt exie
Which users used Export Report most this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can use feature usage while it works on a
feature:

```prompt agent
Look up the Exceptionless feature usage for "Export Report" over the last 30 days, then add feature usage tracking for
the other export options in this repository using the same naming style.
```
