---
title: "Weekly Update - Review of New Releases and More"
date: 2017-04-25
---

# Weekly Update - Review of New Releases and More

Last week we [announced release notes](/news/2017/2017-04-21-exceptionless-net-client-and-core-releases-bugs-usability-performance-self-hosting) for Exceptionless 4.0.2, Exceptionless.NET 4.0.3, and Exceptionless.UI 2.6.2. In our weekly update this week, we review some of those changes/updates, Foundatio changes, and more. Check it out!

## Exceptionless Live Stream Update 4/13/17

### Exceptionless Core Updates

In Exceptionless Core, we added support for [MailKit](https://github.com/jstedfast/MailKit), pipeline action and plugin level metrics, and the ability to dynamically shut down functionality via the configuration. We also made pipeline performance improvements. There are more performance improvements to make,

### Foundatio Updates

For [Foundatio](https://github.com/FoundatioFx/Foundatio), we updated the Azure storage copy implementation to copy server side, fixed an issue where FolderFileStorage wasn't behaving properly when renaming files that exist, and fixed a Redis cache client issue where deleting cache items by wild card was erroring out if there were no matching keys.
- Updated the azure storage copy implementation to copy server side.

### Changes to Exceptionless.NET

For the .NET client, we merged in changes to csproj and released version 4.0.3. Make sure to [check out the release notes](/news/2017/2017-04-21-exceptionless-net-client-and-core-releases-bugs-usability-performance-self-hosting) and upgrade!

### Updates to Exceptionless.UI

In Exceptionless.UI, lists of tags now wrap to the next line, and we fixed an issue where long user names would cause the UI to go crazy.

### Updates to Foundatio.Repositories

Here, we fixed a caching issue where you couldn't set the key without enabling cache, and we simplified index creation and stopped using a lock.


## [WATCH NOW](https://youtu.be/rwl4FfyNCtc?list=PLGHP7IVwFs_81fZTMgF7Dm5e0Ax4YvW_V)




  [Watch more Live Code Demos](/category/weekly-updates/)





