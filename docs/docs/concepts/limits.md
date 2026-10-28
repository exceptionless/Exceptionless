---
title: "Limits and deduplication"
description: "Plan limits, data retention, throttling, what happens at the monthly limit, and how clients and the server keep bursts of events from using up your plan."
navTitle: "Limits"
redirectFrom:
  - from: /docs/deduplication/
    anchor: deduplication
sources:
  - docs/_data/plans.json
  - src/Exceptionless.Core/Billing/BillingPlans.cs
  - src/Exceptionless.Core/Billing/BillingManager.cs
  - src/Exceptionless.Core/Services/UsageService.cs
  - src/Exceptionless.Core/Jobs/EventPostsJob.cs
  - src/Exceptionless.Core/Jobs/CleanupDataJob.cs
  - src/Exceptionless.Core/Pipeline/001_CheckEventDateAction.cs
  - src/Exceptionless.Core/Pipeline/010_AssignToStackAction.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/0_ThrottleBotsPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/05_CheckForDuplicateReferenceIdPlugin.cs
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Core/Repositories/Queries/AppFilterQuery.cs
  - src/Exceptionless.Web/Utility/Handlers/OverageMiddleware.cs
  - src/Exceptionless.Web/Utility/Handlers/ThrottlingMiddleware.cs
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Web/Api/Handlers/OrganizationHandler.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/usage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/organizations/components/organization-notifications.svelte
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/910_DuplicateCheckerPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Queue/DefaultEventQueue.cs
  - Exceptionless.JavaScript:packages/core/src/plugins/default/DuplicateCheckerPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/queue/DefaultEventQueue.ts
---

# Limits and deduplication

Each organization's plan sets how many events it can store each month and how long they're kept. This page explains
those limits, how Exceptionless spreads your events across the month, what happens when you reach a limit, and how the
clients and the server keep a burst of identical errors from using up your plan.

## Plan limits

| Plan        | Events per month | Retention | Projects  | Users     |
| ----------- | ---------------- | --------- | --------- | --------- |
| Free        | 3,000            | 3 days    | 1         | 1         |
| Small       | 15,000           | 30 days   | 5         | 10        |
| Medium      | 75,000           | 90 days   | 15        | 25        |
| Large       | 250,000          | 180 days  | Unlimited | Unlimited |
| Extra Large | 1,000,000        | 180 days  | Unlimited | Unlimited |
| Enterprise  | 3,000,000        | 180 days  | Unlimited | Unlimited |

Yearly plans have the same limits as their monthly versions. See [pricing](/pricing/) for prices and
[Organizations and billing](/docs/organizations/) to change plans. Exie's monthly AI allowance also depends on the plan;
see [Exie](/docs/ai/exie/).

- Limits apply to the whole organization, across all of its projects.
- The event count resets at the start of each calendar month, in UTC.
- The user limit counts pending invites as well as members.
- You can't create another organization while you belong to one on the Free plan.

### What counts as an event

An event counts against the monthly limit when it's processed and stored. These don't count:

- Events for stacks marked **Discarded**.
- Events that the server throws away: those removed by [bot protection](#bots-and-spam), events dated outside the
  [submission window](#retention), and events that repeat a reference ID.
- Events that a client filters out before sending them, such as logs below the minimum log level or events excluded by a
  plugin.

An event that the client has [merged from duplicates](#deduplication) counts once, however many occurrences it stands
for.

To see where your events went, open the organization's **Usage** page. The chart shows **Total**, **Blocked** (rejected
because of a limit), **Discarded**, **Too Big**, and **Deleted** events against the **Limit**. Each project has its own
**Usage** page too.

## Throttling

If an app sent its whole month's events in one afternoon, you'd have no events left for the rest of the month. To
prevent this, Exceptionless counts events in five-minute windows and caps each window at 10 times the even share of the
month:

```text
events per five minutes = 10 × monthly event limit ÷ five-minute windows left in the month
```

For example, on the Medium plan at the start of a 30-day month, that's about 87 events per five minutes. The cap rises
as the month goes on. Plans with fewer than 5,000 events a month, such as Free, aren't throttled, and no plan is
throttled during the last day of the month.

When a window's cap is reached, new events are rejected until the next window starts, and the app shows **Events are
currently being throttled** for the organization. Rejected events show up as **Blocked** on the **Usage** page.
Upgrading raises the cap right away.

## Reaching the monthly limit

When the organization reaches its monthly limit, the API rejects new events until the next month starts or you upgrade.
The app shows that the organization has reached its monthly plan limit, and Exceptionless emails the organization's
users once that month. Events you've already stored stay available, and nothing is billed for events over the limit.

### How clients respond

The API answers rejected event submissions with `402 Payment Required`, both while you're throttled and at the monthly
limit. The clients then stop sending for a while and drop the events they had queued, so that once sending resumes the
events you get are recent ones:

- The .NET client pauses for five minutes and discards events submitted in that time.
- The JavaScript client pauses until the next quarter hour and discards events submitted in that time.

Your app keeps running normally the whole time.

### Other API limits

- **Request rate:** each organization's API keys, and each signed-in user, can make 3,500 API requests per 15 minutes.
  Requests over the limit get `429 Too Many Requests`. Responses include `X-RateLimit-Limit` and `X-RateLimit-Remaining`
  headers. Client configuration and session heartbeat requests don't count.
- **Request size:** an event submission larger than 200,000 bytes gets a `413` response. The clients retry with smaller
  batches, and drop a single event that is still too large.

See the [API overview](/docs/api/) for status codes, and the [client specification](/docs/reference/client-spec/) for
how clients retry.

## Retention {#retention}

Events older than your plan's retention period are no longer shown in the app or returned by the API. Exceptionless
deletes them later, after the retention period of the next plan up, or after 180 days on the largest plans. If you
upgrade before then, the older events become visible again.

Stacks are kept longer than their events. An open stack with no reference links is deleted 60 days after its last event
is deleted. Stacks with another status, such as fixed or discarded, and stacks with reference links are kept, so their
status still applies when the error comes back.

Events must also arrive within three days of when they happened. The server discards events dated earlier than that, or
earlier than your retention period, and changes dates in the future to the current time. So events that wait in a
client's offline queue for more than three days are discarded when they arrive.

Deleting stacks or resetting a project's data doesn't give back events already counted for the month.

## Deduplication {#deduplication}

When the same error happens many times in a few seconds, such as an exception thrown in a loop, the clients merge the
repeats into one event with a count:

1. The first occurrence is sent right away.
2. Identical occurrences within the window are held and counted instead of being sent.
3. When the window ends, the client sends one event with the total in its `count` field and the date of the latest
   occurrence.

The merged event counts once against your plan, and the event counts in the app include its full `count`.

|                     | .NET client                                                                              | JavaScript client                                            |
| ------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| Events deduplicated | All types                                                                                | Errors only                                                  |
| Identical means     | Same type, source, message, tags, value, and data, such as the error and request details | Same error messages and stack traces, including inner errors |
| Window              | 60 seconds                                                                               | 30 seconds                                                   |

Each client remembers the last 50 events it sent. Deduplication is done by the `DuplicateCheckerPlugin` in both clients.
To turn it off, remove the plugin: `RemovePlugin<DuplicateCheckerPlugin>()` on the .NET configuration, or
`removePlugin("DuplicateCheckerPlugin")` on the JavaScript configuration.

The server also discards an event whose [reference ID](/docs/references-ids/) was already used in the same project in
the last day, so a client that retries a submission doesn't store the event twice.

## Bursts

Deduplication and throttling together handle most bursts. When an outage throws thousands of errors a minute:

- Identical errors from one process are merged by the client.
- The same error from many processes or servers isn't merged, because each client deduplicates on its own, but it still
  goes into one [stack](/docs/concepts/stacking/), so it's easy to review.
- Throttling stops the burst from using the rest of the month's events.
- The client pauses after a `402`, so your app doesn't keep sending events that will be rejected.

To keep a known noisy error from counting at all, mark its stack **Discarded**. See [Stacks](/docs/managing-stacks/).

### Bots and spam {#bots-and-spam}

Two project settings deal with traffic from crawlers and attackers, such as a scanner that causes hundreds of 404s. Find
them on the project's **Settings** page under **Spam Detection**:

- A list of user agents to ignore. The clients drop events from requests with a matching user agent before sending them.
- **Reduce noise by automatically hiding high volumes of events coming from a single client IP address.** When a single
  public IP address causes 25 or more events within a five-minute window, the server discards them and removes the
  events already stored from that address in that window. This needs IP addresses, so it has no effect when the project
  removes user identifiable information.

See [Projects](/docs/project-settings/) for both settings.

## Self-hosted limits

On a self-hosted instance without billing, new organizations get an unlimited plan: no event limit, unlimited projects
and users, and retention up to the instance's maximum (180 days by default). The request rate, request size, bot
threshold, and maximum retention are settings; see [Configuration](/docs/self-hosting/configuration/).
