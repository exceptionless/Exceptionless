---
title: "Email Logging, UI Improvements, Foundatio Updates, and more - Live Code Demo"
date: 2017-03-08
---

# Email Logging, UI Improvements, Foundatio Updates, and more - Live Code Demo

[![Exceptionless live code demo 2/20/17](/assets/img/news/live-code-demo-170308-1024x538.jpg)](https://www.liveedu.tv/niemyjski/2qyKy-exceptionless-weekly-demo-2-20-17/xAq0E-exceptionless-weekly-demo-2-20-17/)

Watch out this week, Blake's on fire! We're talking email loggings, UI tweaks, Exceptionless.NET updates and fixes, Foundatio updates, and Foundatio.Repositories updates. Lot's going on, let's check it out in [this week's Live Code Demo](https://www.liveedu.tv/niemyjski/2qyKy-exceptionless-weekly-demo-2-20-17/xAq0E-exceptionless-weekly-demo-2-20-17/).

## Exceptionless Updates

This week, we made improvements to email logging and documentation when running in dev mode. Find out more by watching the live coding video or visiting the [Exceptionless repo on GitHub](https://github.com/exceptionless/Exceptionless).

## Exceptionless

We were only showing the exception tab if there was an event type of error, previously, but now we are always showing the exception tab if an exception is part of an event.

[View Repo](https://github.com/exceptionless/Exceptionless)

## Exceptionless.NET

* We added SetException EventBuilder overload so you can submit any event type with an exception object.
* Then, we fixed an issue where the client could blow up on startup while trying to wire up to trace listeners.
* Finally, we also fixed an issue where signed web packages http handlers weren't being registered with the right namespace.

[View Repo](https://github.com/exceptionless/Exceptionless.Net)

## Foundatio

* Attempted to track down issues where the redis queues would stop processing.
* Worked with Microsoft to get our unit tests discoverable on the new csproject format.
* Added new [Foundatio.Jobs commands package](https://github.com/FoundatioFx/Foundatio/commit/50dddaa52d3cc929a62d42b40f8d767e4f916545) that allows you to quickly discover and get command line help on your jobs.

[View Repo](https://github.com/FoundatioFx/Foundatio)

## Foundatio.Repositories

* Fixed various issues with running data migration scripts for the first time.
* Fixed an issues with GetByIds where falling back to search wouldn't take into account multiple pages of results.

[View Repo](https://github.com/FoundatioFx/Foundatio.Repositories)


## [WATCH NOW](https://www.liveedu.tv/niemyjski/2qyKy-exceptionless-weekly-demo-2-20-17/xAq0E-exceptionless-weekly-demo-2-20-17/)




  [Watch more Live Code Demos](/category/live-coding/)


