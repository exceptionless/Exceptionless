---
title: "Foundatio Featured on .NET Blog + Version 4.0 Release!"
---

# Foundatio Featured on .NET Blog + Version 4.0 Release!

![Foundatio 4.0 release](/assets/img/news/foundatio-4.0.png)We've continued to focus some of our attention on our pluggable app foundation block solution, Foundatio, and last week it got some attention from the Microsoft .NET Blog as well!

[Foundatio featured as Project of the Week on .NET Blog](https://blogs.msdn.microsoft.com/dotnet/2016/03/29/the-week-in-net-3292016/)

We've also released V4.0, which has some new implementations, API changes, and, of course, bug fixes.

Check out the details, below.

## Foundatio 4.0 Release Notes

### Implementations

We've got a few new implementations with this release, including Amazon S3 storage, Azure Service Bus queue (thanks [@jamie94bc!](https://github.com/jamie94bc)), and Redis metrics client. Users should find those helpful.

### API

The APIs have been drastically simplified for ease of use and straightforwardness, which should allow for easier creation of new implementations and consumption of existing ones.

Once example of this simplification is that we've paired down jobs to having only one lock, instead of four.

Logging has also seen huge improvements to make sure we are aligned with future API implementations, etc.

### Bugs

More tests have been added since the previous release, and we've fixed a few major bugs as well.

* Deadlocks were occurring in a few areas, which is now remedied.
* Dropped connections are now automatically healed, while previously we were seeing some issues.
* Cached reference types can no longer be updated.

## Upgrading to Foundatio 4.0

If you're already using Foundatio, simply updated your NuGet package. If you're new and want to check it out, snag [Foundatio over on GitHub](https://github.com/FoundatioFx/Foundatio).

And, as always, we're here to answer any questions or take any feedback you might have to offer!
