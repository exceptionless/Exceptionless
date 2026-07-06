---
title: "New Releases for ALL the Codes! Exceptionless 3.2"
---

# New Releases for ALL the Codes! Exceptionless 3.2

![Exceptionless 3.2 Highlights](/assets/img/news/exceptionless-3-2-release-notes.png)That's right folks - we've gone and released Exceptionless 3.2, which includes releases for Exceptionless.NET, Exceptionless.JavaScript, and Exceptionless.UI! Awe yeah.

We're kind of excited, in case you couldn't tell. Big stuff in here, like **session tracking** (#BOOM), licensing changes (less confusion - it's a good thing), and **posting via HTTP GET** (such easy, much wow)!

Lets get into some of the details...

## Exceptionless 3.2.0

###

### Sessions!

**Track and automatically manage user sessions** for much more visibility into their user experience, how they interact with your app, and, of course, any errors or events that occur related to that user. This answers the age-old question, "What the hell was this guy doing when stuff blew up!?"

[Check out the User Sessions post for more details and instructions!](/track-view-user-session-data-exceptionless/)

![Exceptionless Event Sessions](/assets/img/news/sessions-2.png)






### HTTP GET!

Now it's even easier to **integrate with Exceptionless from any environment**, because you can post event or meta data via HTTP GET! More info coming soon (blog post).

### License Change

The server and all Exceptionless projects are now using the [Apache License](https://github.com/exceptionless/Exceptionless/blob/master/LICENSE.txt), so there should be much less confusion on how things are licensed moving forward. Boring stuff, we know... but important.

### User Location

User locations are now resolved from geographic coordinates or the IP address. We look at the `geo` property for coordinates or an IP, then we inspect the IP. If no IP or geo coordinates present themsevles, we fall back to the client IP that the event was submitted from.

### More Speed Improvements

As always, we keep speed improvements in mind with each release. With 3.2, we've been able to make more **massive improvements in processing time for events** (over 250% per 1000 events!) and further reduce app startup times and elastic query execution times. #alwaysoptimizing!

### Hourly Throttling

The hourly event-throttling threshold has been **increased** from 5x to 10x the plan limit. The way we calculate it is by taking the plan limit and dividing it by the hours in the month, then multiplying it by 10.

### Signup Experience

The signup experience has been improved when inviting users, as well. Thanks [@mcquaiga](https://github.com/mcquaiga) and [@theit8514](https://github.com/theit8514) for your contribution!

### Upgrading (Self Hosters)

**Self hoster?** Need to upgrade? [The latest code can be downloaded from the release page.](https://github.com/exceptionless/Exceptionless/releases/tag/v3.2.0) All other users: No action required.

* * *

## Exceptionless.UI 2.3.0

User experience was the primary focus of this UI release, along with the new sessions feature. More details below, including other improvements and a few bug fix details.

### Adding a New Project

When adding a new project, users will now have a much better experience, and we added a JavaScript configuration section for JS projects. Check it out!

### Reference id Lookup

Support for looking up reference ids was added, so you can now navigate to `/event/by-ref/YOUR_REFERENCE_ID` to look up an event.

### Other Improvements

* Better messages and a loading mask has been added to data grids to improve user experience when filtering and loading data.
* Escaping of strack traces containing HTML or CSS has also been improved.
* You can now sort extended data items alphabetically.
* The request and environment info tabs for events now show additional extended data.

### Bug Fixes

* You can now create an organization or project that ends with a period or whitespace.
* Sometimes an incorrect time range would be set when users used the history chart/graph to select a period of time to drill down to.

Check out the [Exceptionless.UI Changelog](https://github.com/exceptionless/Exceptionless.UI/compare/v2.2.0...v2.3.0) for all the code changes (87 files / 75 commits).

* * *

## Exceptionless.NET 3.3.2

Users on desktop applications can now opt-in to sessions by setting a default user and calling the below:

```cs
ExceptionlessClient.Default.Configuration.UseSessions();
```

Also, module info was not being included in some error reports, which was incorrect. That has now been fixed.

The [full change log](https://github.com/exceptionless/Exceptionless.Net/compare/v3.3.1...v3.3.2) can be viewed on GitHub.

* * *

## Exceptionless.JavaScript 1.3.1

Besides integrating with the above, the only major change in the JavaScript client, like the .NET client, was that users can now op-in to sessions. To do so, set a default user and call the below:

```cs
exceptionless.ExceptionlessClient.default.config.useSessions();
```

Check out the [full change log](https://github.com/exceptionless/Exceptionless.JavaScript/compare/v1.3.0...v1.3.1) for all the dirty details.
