---
title: "Exceptionless 3.4 - New User Dashboards, Job Reliability, and Bug Fixes"
---

# Exceptionless 3.4 - New User Dashboards, Job Reliability, and Bug Fixes

![Exceptionless 3.4](/assets/img/news/exceptionless-3-4-header.png)(/assets/img/news/exceptionless-3-4-header.png)

The latest Exceptionless release has several additions we think most of our users will find helpful. We sat down and worked on the UI, fixed some bugs, and spend a considerable amount of time improving reliability and efficiency of some of the primary pieces of the app.

If you're a self hoster, you'll need to [upgrade your existing install](/docs/self-hosting/upgrading-self-hosted-instance), but if you're hosting with us there is no action required on your part to experiences the Exceptionless 3.4.

For more information about this release, take a look below and/or review the [full release notes over on GitHub](https://github.com/exceptionless/Exceptionless/releases/tag/v3.4.0).

## UI Updates

These updates were all pushed with [Exceptionless.UI 2.5](https://github.com/exceptionless/Exceptionless.UI/releases/tag/v2.5.0) a few days prior to this release of the main app. Enjoy!

### Search Wildcard

You can now use `*` to show **all** events in the search box. Woohoo!

### Most Users Dashboard

The new most users dashboard allows you a quick view of events sorted by the highest number of affected users. This is great for helping prioritize your work pipeline.

Also, as an aside, we've added the users affected column to the dashboard. We know some of you guys will find that helpful.

### New Keyboard Shortcuts

MacOS & Linux keyboard shortcut support has been added, as well as additional shortcuts such as `C` to chat with support, `S` to focus the search bar, and `g` `a` to go to your account. Hit `SHIFT` + `/` (also known as `?` ) to access the keyboard shortcut list on any screen.

As an aside here, there is also now a `</>` button near the top of the event occurrence that lets you quickly copy the JSON to your clipboard with a click.

## Other Updates

This is just a quick list of everything else we tweaked, updated, added, or fixed with the v2.4 release.

### Performance & Reliability

We made several reliability and performance enhancements to queue and job processing. A few specific examples include fixing auto-abandoned jobs and instances where batch events weren't being requeued.

### Heartbeat API Endpoints

Previously we had [worked on making heartbeat events efficient](/news/2016/2016-05-26-session-heartbeats-no-longer-count-towards-plan-limits) so we didn't have to count them toward event quotas, and with this release we've added new API Endpoints that allow clients to submit those heartbeats cheaply.

### Active Directory Authentication

Support has been added for Active Directory Authentication. Thanks [@laughinggoose](https://github.com/laughinggoose)! To enable this feature, head over to the [Active Directory Authentication](/docs/self-hosting/kubernetes#active-directory-authentication) documentation page on GitHub.

### Count

This `Count` property was added to the event model that tracks deduplicated events and allows for some pretty cool metrics from here on out while avoiding the full cost of storing every event.

### MaximumRetentionDays

`MaximumRetentionDays` is pretty self explanatory. It controls the max retention perdiod for events, which allows the retention job and plans to be smarter about cleaning up old data.

### Bugs

SignalR (web sockets) support wasn't always working in some hosting environments such as AWS, so we fixed a few bugs related to that.
