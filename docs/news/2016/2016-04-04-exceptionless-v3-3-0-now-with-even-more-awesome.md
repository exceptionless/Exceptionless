---
title: "Exceptionless V3.3.0 Release - Now with Even More Awesome"
---

# Exceptionless V3.3.0 Release - Now with Even More Awesome

![Exceptionless 3.3.0 Release Notes](/assets/img/news/Exceptionless3-3-0-release-notes.jpg)

We always work hard to keep improving Exceptionless, and this release is no different!

Since the last release, we've put a lot of time into [making the filtering and searching more user friendly](/news/2016/2016-03-24-filter-improvements-exceptionless-single-page-app) and intuitive, improving the reliability of jobs, and of course fixing any bugs that you guys (or Exceptionless) have been able to find.

Let's see what we've got going on, shall we?

## What's Goin On?

### Filter / Search

For starters, as mentioned above, we relocated the search bar to exist on the top level of the UI, and the date picker filter now shows the current choice on the top level. Both icons were replaced, and we really think it's much more intuitive and efficient. You can read more and see examples over on the [dedicated blog post](/news/2016/2016-03-24-filter-improvements-exceptionless-single-page-app) we did last week.

### New Stats API

You can now get a timeline or numbers for a comma delimited list of fields using the new stats API, which is pretty cool.

### Session Management

Session management has been drastically improved by doing a few different things. For instance, inactive sessions are now closed faster, but they can be opened again if need be. We hid heartbeat events by default, too, and you can now specify manual sessions for desktop-based apps.

### Manual Stacking

We introduced [custom event stacking](/docs/manual-stacking) a few weeks ago, and [@adamzolotarev](https://github.com/adamzolotarev) has added the ability to specify a manual stacking key on the client side with this release. Thanks Adam!

### Discard events created from bots

A default list of bot wild card exclusions is now automatically set on new projects, so if you're upgrading, you now have the ability to run a maintenance job via the admin controller to set a default bot list. All events with user agents matching these wild cards will then be discarded on the client side.

### Bugfixes!

* Marking stacks as fixed or hidden was causing some significant slow down and sometimes wouldn't work at all. This has been remedied!
* Redis connection failures and lock timeouts were sometimes causing jobs to stop working or fail. We dug through and found what was causing that and fixed it as well.
* When the geo field contained a localized number, sometimes events were not being processed. This [localization](/news/2016/2016-02-10-add-reverse-geocoding-to-your-app) issue has been solved.
* And last but not least (well, maybe least), a seralization bug has been fixed that would cause query strings, cookies, and other extended data items to be transformed to lowercase and underscored.

### Time to Upgrade

Well, only if you're a self hoster. Everyone else will experience all of these awesome improvements and bug fixes the next time they log in. If you are a self hoster, please review the [Self Hosting Docs](/docs/self-hosting/) for info regarding upgrading your current Exceptionless install. Naturally, if you have any questions please let us know and we will get you taken care of.

## In Conclusion

You can find a complete comparison changelog over on [GitHub](https://github.com/exceptionless/Exceptionless/compare/v3.2.1...v3.3.0), where you can also submit any issues, etc if you run across anything. Please also let us know what you think of the changes by commenting below, pinging us on social media, or simply sending Blake a [glitter bomb](http://www.ruindays.com/products/spring-loaded-glitter-bomb) _(site/link not endorsed in any way, lol - first one I found!)_.
