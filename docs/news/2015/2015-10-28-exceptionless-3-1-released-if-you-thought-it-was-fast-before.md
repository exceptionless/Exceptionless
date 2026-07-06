---
title: "Exceptionless 3.1 Released! If You Thought It was Fast Before..."
date: 2015-10-28
---

# Exceptionless 3.1 Released! If You Thought It was Fast Before...

![exceptionles-3-1-header](/assets/img/news/exceptionles-3-1-header.png)If you thought Exceptionless was fast before, **prepare to have your mind blown** by what we've been able to do in version 3.1, which released today.

In short, we've reduced cpu and memory usage, increase caching efficiency, and sped up searching, all quite significantly as you'll see below.

Along with these speed boosts, we've also made Job improvements (look for a blog post on this soon) and upgraded to .NET 4.6 (**self-hosters**, please install .NET 4.6 on your servers before upgrading).

Details on the release changes can be found below.

## It's All About Speed!

### CPU & Memory

We reduced the CPU and Memory load **across the entire app**. This allows the application to use fewer resources, meaning it has more time to process additional events, making everything faster. Between making Exceptionless and [Foundatio](https://github.com/FoundatioFx/Foundatio) 100% Async and these improvements, we've drastically increased the efficiency of the entire platform.

**Below**, we see the increase in performance from two examples. On the left, we see a reduction in CPU and Memory usage for a deployed web app instance. On the right is a visible reduction in CPU usage for an Elasticsearch node.

![cpu-memory-percentage-improvements](/assets/img/news/cpu-memory-percentage-improvements-e1446046152986-1024x343.png)

### Elasticsearch Queries

By profiling the Elasticsearch queries for efficiency and usage, we've been able to reduce the overall number we were running and improve the efficiency on the ones that still are.

![search-request-rate-improvements](/assets/img/news/search-request-rate-improvements-e1446046076483.png)

### Caching

Caching efficiency has been improved by removing redundant components that were utilizing valuable resources. For example, we removed the SignalR Redis Backplane, which drastically decreased the number of calls to Redis. Overall, we've made the app smarter throughout regarding how we cache and retrieve data.

![cache-improvements-2](/assets/img/news/cache-improvements-2-e1446046201498-1024x346.png)

![Caching-improvements](/assets/img/news/Caching-improvements-e1446046263253-300x224.png)

### Long-running API Tasks

We've offloaded long-running API tasks to background jobs, freeing up a lot of resources in the app and allowing us to scale work items independently. For example, marking a stack as fixed or removing a project may take a few moments to be updated now, but the trade-off is worth it. We're working on updating the UI experience to prompt users that the task is running in the background.

## Other Improvements

### Jobs

We've made each Job a console app, so it's much easier to debug and deploy. Look for a full blog post on Job improvements soon.

### .NET 4.6

Exceptionless is now running on .NET 4.6, which has improved startup time due to various improvements with the new version. **Self-hosting users** should be sure to upgrade to .NET 4.6 on their servers before updating Exceptionless.

## Upgrading

For full release notes and to download the latest version, please visit the [GitHub Release Page](https://github.com/exceptionless/Exceptionless/releases).

## Always Improving

**We're always striving to improve** the efficiency of Exceptionless and all of our projects. If you see any room for improvement or have any comments when using anything from us, please send us an in-app message, submit a GitHub issue or [contact us](/contact/) on the website.
