---
title: "Introducing Foundatio 3.0! Now with Async & Increased Efficiency"
---

# Introducing Foundatio 3.0! Now with Async & Increased Efficiency

![Foundatio 3.0](/assets/img/news/foundatio-blog-header-image-small.png)Foundatio is a pluggable, scalable, painless, open source solution for caching, queues, locks, messaging, jobs, file storage, and metrics in your app.

In Version 3.0, we've made several improvements, including, as promised in our [initial Foundatio blog post](/news/2015/2015-07-14-foundatio-pluggable-blocks-building-distributed-apps), going full async.

Take a closer look at the new enhancements, below, and [head over to the GitHub repo](https://github.com/FoundatioFx/Foundatio) to try Foundatio today. We think you'll find it very handy.

For all our current users, you'll need to upgrade your [Foundatio NuGet package](https://www.nuget.org/packages?q=Foundatio) and existing Foundatio code to use the async implementations/conventions. The update should be fairly straightforward - we haven't had or heard of any issues so far, including when upgrading Exceptionless.

## What's New in Foundatio 3.0

### API Changes

We've gone **100% async**, which means no more blocking synchronous code, allowing your apps to be even more responsive. For more on async and its benefits, [take a look at this Microsoft article](https://msdn.microsoft.com/en-us/library/vstudio/hh191443(v=vs.140).aspx).

Along with going completely async, we've **removed non async methods** from the API and **simplified the API overall**. We wanted to make it easy for developers to roll out new implementations and use the existing API.

#### New methods

We also **added new, useful methods** across all the APIs to make it easier to perform common tasks. One example includes adding CalculateProgress() to WorkItemHandlerJobs. We hope these helpers allow you to be more productive.

### Efficiency Improvements

Because we always want more speed out of our apps (and we know you do, too), we used Red Gate's [ANTS Performance Profiler](http://www.red-gate.com/products/dotnet-development/ants-performance-profiler/) to profile various apps, such as Exceptionless, and track down "hot" sections of code. By hot, we mean those pieces of code devoured more resources and took longer to run than all other application code. By doing so, we were able to pinpoint core pieces of Foundatio that were performing poorly compared to what they could be doing. **Then we fixed them.**

#### A few optimization examples

* **Maintenance code** in various locations like queues, caches, etc, could and would run in a tight loop, sometimes pegging the CPU to 100% depending on the configuration. By running through and removing that maintenance code, or changing the way it was implemented to be triggered only when needed, rather than running in a constant worker loop, we drastically reduced CPU load and increased efficiency.
* We also made **massive improvements to queues and locks**, on top of the maintenance code changes. Our Dequeue and AcquireLock methods would constantly pull for new items in a loop with a small timeout, which is obviously inefficient. Now we are using [async monitors](https://github.com/StephenCleary/AsyncEx/wiki/AsyncMonitor) and pulse when a message is received over the message bus. This allows for **huge improvements** as the code will wait/sleep for a single monitor (of multiple monitors that could be waiting) to get triggered. This means your app isn’t wasting CPU cycles waiting, or external network calls looking for locks or new queue items. Instead, it’s running your app code and getting immediately notified when the lock is available or a new queue item is enqueued. Pretty slick!
* **Caching** got a huge performance boost, too. For InMemoryCache clients, we moved from reflection-based object copying to using [Expression Trees](https://msdn.microsoft.com/en-us/library/bb397951.aspx). This reduced the time required to get items from cache by a large percentage. Read more about implementing Expression Trees and see the difference it makes [here](http://blog.nuclex-games.com/mono-dotnet/fast-deep-cloning/).

## Check It Out - Feedback Appreciated!

We made many other improvements to ensure your apps run fast when using Foundatio, too many to be listed here. If you're already using it, just update your NuGet package to the latest version to take advantage of these improvements. If you're not using it yet, [try it out](https://github.com/FoundatioFx/Foundatio). It's worth a shot, we promise!

Once you've given it a go, please let us know what you like, or what you hate, by posting a [issue on Foundatio's GitHub Repo](https://github.com/FoundatioFx/Foundatio/issues).

Until next time, happy coding!
