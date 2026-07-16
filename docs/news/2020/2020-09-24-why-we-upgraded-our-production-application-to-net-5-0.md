---
title: "Why We Upgraded Our Production Application to .NET 5.0"
date: 2020-09-24
---

# Why We Upgraded Our Production Application to .NET 5.0

---

> ## Update: Microsoft has [officially released .NET 5.0!](https://devblogs.microsoft.com/dotnet/announcing-net-5-0/)

---

For anyone who has built an application, you've probably built it on some library or framework that changes over time. To keep up, you have to upgrade your application. However, there are varying schools of thought around when you should upgrade. At [Exceptionless](https://exceptionless.com/), we like to be on the bleeding edge. As an open-source company, we feel a responsibility to the community to know and understand the open-source tools we use. As such, we have already upgraded Exceptionless to use .NET 5.0.

To give you a little background, .NET 5.0 was [introduced in May of 2019](https://devblogs.microsoft.com/dotnet/introducing-net-5/). The announcement was a big one as Microsoft chose to drop the .NET Core distinction. Going forward, we will just see cross-platform support in the form of ".NET X.X". The first release candidate for .NET 5.0 was [announced September 13, 2020](https://devblogs.microsoft.com/dotnet/announcing-net-5-0-rc-1/). We chose to upgrade and begin using .NET 5.0 immediately. That decision was driven by Microsoft's commitment to supporting production usage of the rc1 release. And as it turned out, the upgrade process was not too painful at all.

All in, the upgrade took about one hour and was a very small commit. You can actually [see the commit here](https://github.com/exceptionless/Exceptionless/commit/874f08e70a3ded2762f8d34df0378de38d7a3193). This is really a testament to the foundation we've built here combined with the long-running foundation Microsoft has built with the .NET framework. Exceptionless is no small application and yet we were able to upgrade to an early release candidate in order to capitalize on new capabilities. To highlight the scale of Exceptionless and the relatively minor impact the upgrade process had, let's take a look at some of our numbers.

* 1.4 TB Elasticsearch Cluster
* 173M Elasticsearch Documents
* 384M Redis Operations/Day
* 122M HTTP Requests/Day
* 2,476 GitHub Stars
* 568 GitHub Forks

There are always multiple schools of thought around running pre-release code on production applications, but for us, the decision was a no-brainer. The top motivators were performance improvements, availability of new C# features, and Docker improvements for our self-hosted solution.

## Performance Improvements

We are a developer tool, and as such, performance is important. .NET 5.0 allows us to leverage the performance boosts associated with the upgrade and pass that along to our customers and the community around us. We compared our memory and performance from .NET Core 3.1 to the .NET 5.0 rc-1 release and saw enough gains to help support our decision to move forward with rolling this out to production.

The .NET team's focus on pushing the boundaries of garbage collection was an important factor for us. GC is such a critical component to performance, and it impacts almost everything within the framework. We were excited to see the [focus Microsoft put on continuing to improve performance](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-5/#gc) in this area and felt the gains were enough to really tilt us towards our production release of Exceptionless using .NET 5.0.

As a quick, visual example, of other improvements, here's a table Ben Adams tweeted that gives us glimpse into the performance gains of .NET 5.0 over .NET 3.1

<https://twitter.com/ben_a_adams/status/1306817978927902720>

## New C# Features

With the release of C# 9, we, once again, get significant improvements. Anytime a programming language releases new features, it's important to ask yourself whether those features are necessary for your application. In the case of C# 9, there are multiple features we believe will help improve the code legibility, overall codebase size, and ultimately performance. It always comes back to performance!

Pattern matching in C# 9 is a feature we are particularly excited about. If you're interested in a deep dive into the improvements here, [Anthony Giretti has a great post](https://anthonygiretti.com/2020/06/23/introducing-c-9-improved-pattern-matching/) highlighting the new functionality. For Exceptionless, pattern matching represents a better way for us to execute logical operations we already support. In doing so, we can reduce code complexity, improve performance, and deliver a better experience.

Records are an exciting new feature in C# 9 as well. Data immutability is important, once again, for—you guessed it—performance. The way [Dave Brock puts it](https://daveabrock.com/2020/07/06/c-sharp-9-deep-dive-records) on his blog is apt:

> Immutable types reduce risk, are safer, and help to prevent a lot of nasty bugs that occur when you update your object.

Data records give us immutability in the form of a dedicated struct. Rather than extending the functionality of C#'s existing structs, records give us the ability to reach for a data-specific type that offers built-in immutability.

## Docker Improvements

We are proud of our open-source roots. We want to make self-hosting Exceptionless as easy as possible, and Docker has made this a reality. Our Docker image is the fastest and easiest way to get started with self-hosting and .NET 5.0 only improves the Docker experience.

.NET 5.0 enables better resource compaction which, in turn, reduces the cost associated with Docker images. This is important to the bottom line, but .NET 5.0's improvements go beyond dollar-savings with Docker. The new features also improve memory constraints which—say it with me now—improve performance.

One additional benefit of the .NET 5.0 rc-1 release candidate is how our Docker image now works better with Kubernetes resource constraints. We're pretty big fans of Docker and Kubernetes, so anything to improve the experience around both is a win in our eyes.

## Conclusion

We took an early release candidate from a massive framework and rolled it out to production almost immediately after it was announced. Are we crazy? We don't think so, but you decide.
