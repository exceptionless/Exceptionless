---
title: "FAQ"
metaTitle: "Exceptionless frequently asked questions"
description: "Short answers to common Exceptionless questions, with links to the pages that cover each one: performance, offline apps, limits, bursts, and testing."
sources:
  - Exceptionless.Net:src/Exceptionless/Queue/DefaultEventQueue.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.JavaScript:packages/core/src/queue/DefaultEventQueue.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/manage/+page.svelte
  - LICENSE.txt
---

# FAQ

## Will Exceptionless slow my application down?

No. Submitting an event puts it in an in-memory queue, and the client sends the queue in the background, in batches of
up to 50 events every 10 seconds. Your code doesn't wait for the network. See the
[client specification](/docs/reference/client-spec/).

## What happens if my internet connection goes down?

Events stay in the client's queue and are retried later. To keep queued events when the app restarts, turn on persisted
storage: set `StoragePath` in .NET, or `usePersistedQueueStorage` in JavaScript. The server discards events that arrive
more than three days after they happened. See [Retention](/docs/concepts/limits/#retention).

## Why is my organization throttled? {#why-is-my-organization-throttled}

Exceptionless caps the events each organization can send in a five-minute window, so a burst can't use up a whole
month's events. Upgrading raises the cap. See [Throttling](/docs/concepts/limits/#throttling).

## What happens when the organization reaches its plan limit? {#what-happens-if-the-organization-plan-limit-is-reached}

New events are rejected until the next month or until you upgrade. Your app keeps running, the clients pause sending for
a few minutes at a time, and you're never billed for overage. See
[Reaching the monthly limit](/docs/concepts/limits/#reaching-the-monthly-limit).

## What happens if my app throws thousands of errors in a short time? {#what-will-happen-if-my-application-throws-a-bunch-of-exceptions-in-a-very-short-amount-of-time}

The client merges identical errors into one event with a count, the errors are grouped into a stack, and throttling
keeps the burst from using up your plan. See [Deduplication](/docs/concepts/limits/#deduplication) and
[Bursts](/docs/concepts/limits/#bursts).

## Why did these errors end up in the same stack, or in different stacks?

Errors are grouped by a signature, usually the exception type and the method in your code that threw. Open the stack and
choose **View Stacking Information** to see the values. See [Stacking](/docs/concepts/stacking/).

## Can I reset my event data? {#can-i-reset-my-event-data}

Yes. To delete all of a project's events and stacks, use the **Reset project data** button (the warning icon) on the
project's **General** settings page. To delete one stack and its events, choose **Delete** from the stack's options
menu. Neither gives back events already counted toward this month's limit. See [Projects](/docs/project-settings/) and
[Stacks](/docs/managing-stacks/).

## How do I turn off event reporting during tests or local development? {#how-can-i-disable-event-reporting-during-testing}

Disable the client. In .NET, set `"Enabled": false` in the `Exceptionless` section of your configuration, for example in
`appsettings.Development.json`. In JavaScript, set `enabled` to `false` in the configuration. See
[.NET configuration](/docs/clients/dotnet/configuration/) and
[JavaScript configuration](/docs/clients/javascript/client-configuration/).

## How do I keep passwords and personal data out of Exceptionless?

Add [data exclusions](/docs/security/#data-exclusions) for sensitive field names, and turn off private information in
the client or the project settings. See [Security and privacy](/docs/security/).

## Can I host Exceptionless myself?

Yes. Exceptionless is open source under the Apache 2.0 license, and you can run it with Docker or Kubernetes. See
[Self-hosting](/docs/self-hosting/).
