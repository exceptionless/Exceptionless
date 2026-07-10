---
title: "New Releases: Exceptionless 3.2.1, .NET Client 3.3.6, JavaScript Client 1.3.2, UI 2.3.1"
---

# New Releases: Exceptionless 3.2.1, .NET Client 3.3.6, JavaScript Client 1.3.2, UI 2.3.1



  ![Exceptionless error logging](/assets/img/news/blog-header-image-3.2.1.jpg)



Since the [last major release](/new-releases-for-all-the-codes-exceptionless-3-2/) cycle, we've made several minor releases, including Exceptionless 3.2.1, Exceptionless.NET 3.3.5, and Exceptionless.UI 2.3.1.

Lets take a look at some of the highlights, and you can check out the full release notes on each at the provided links, below.

## Exceptionless 3.2.1

We fixed a few minor bugs made a few improvements to the main platform. Check them out!

* Free accounts can now look up events by reference ID (bug).
* Improvements to posting events via GET.
* Items can now be added and removed from project data (bug).
* Users can log directly in to an existing account if they attempt to sign up with the same credentials now, rather than getting an error.
* [@VikzSharma](https://github.com/VikzSharma) fixed the "too many bad attempts" lockout feature. Thanks!
* There is also a once-an-hour limit on user signups and email address changes thanks to @VikzSharma.

**Upgrading:** Only self-hosters need to worry about upgrading - see details on the [full release notes.](https://github.com/exceptionless/Exceptionless/releases/tag/v3.2.1)

## Exceptionless.NET 3.3.3 - 3.3.6

We've pushed out several minor releases of the .NET client in the last few weeks. I'll cover the major stuff here, but you can view the [full release notes and get the latest source on GitHub](https://github.com/exceptionless/Exceptionless.Net/releases).

### 3.3.6

* An issue causing query string params and cookies to be renamed when dictionary key names were being changed has been fixed.
* `client.Register()` now respects your session setting.
* Manual stacking now uses a model instead of a string, which lets us send a stack title and key value pairs telling the event how to be stacked. More info on the release page.

**If you are using manual stacking**, this is a required update. [See the release page for the latest source.](https://github.com/exceptionless/Exceptionless.Net/releases/tag/v3.3.6)

### 3.3.5

* New extension methods have been added for events, making it easier to set valid geo coordinates, tags, and more.
* A few new variables and parameters have been added for session heartbeats and id setting. See release notes for details.
* [@InlineASM](https://github.com/InlineAsm) fixed an issue for geolocations that have different separators - thanks!

### 3.3.4

* [@ahmettaha](https://github.com/ahmettaha) added SubmitLog and CreateLog overloads without source parameters. Thanks!
* `ExceptionlessClient.Default.Configuration.UseDebugLogger()` only worked in debug mode with client source, so we replaced it with `ExceptionlessClient.Default.Configuration.UseInMemoryLogger()`
* The serializer wasn't always being passed through so it could get known event data helper methods, which was causing some silent failures - this has been fixed.

### 3.3.3

* [@adamzolotarev](https://github.com/adamzolotarev) added the ability to take complete control over how an event is stacked (be careful) by adding the ability to do manual stacking by calling `EventBuilder.SetManualStackingKey("MyStackingKey")`
* You can now ignore events by user agent bot filter when you call `(ExceptionlessClient.Default.Configuration.AddUserAgentBotPatterns("*bot*"))` on the client side or edit project settings server side.
* The default max size limit of `RequestInfo` has been increased.
* Extra nesting has been reduced by merging `exception.Data` properties into the `Error.Data` dictionary.
* Bug Fix: `AggregatedExceptiones` that contain more than one inner exception are no longer discarded.
* Bug Fix: Machines with a Non-English locale will not process events when `SetGeo` is used.
* Bug Fix: `ArgumentNullException` will no longer be thrown if post data or cookies contain a null key.

## Exceptionless.JavaScript 1.3.2

* [@frankebersoll](https://github.com/frankebersoll) contributed by adding support for offline storage, which can be enabled by calling `client.config.useLocalStorage()`. Thanks!
* User agent bots can be ignored via a filter now with ``(`client.config.addUserAgentBotPatterns("bot"))`` on the client side or via project settings on the server side.
* [@frankebersoll](https://github.com/frankebersoll) also added support for manual stacking (be careful! grants complete control). See release notes for instructions.
* The implementation of the angular `$stateChangeError` has also been improved.

[Full release notes](https://github.com/exceptionless/Exceptionless.JavaScript/releases/tag/v1.3.2)

## Exceptionless.UI 2.3.1

There's nothing major to report with the UI, just a few tweaks.

* The project settings pages has been reworked by adding the ability to specify user namespaces, and user agents that the clients will ignore. @VikzSharma also fixed an issue where the single page app could be clickjacked - thanks again!

[Full release notes and latest release download.](https://github.com/exceptionless/Exceptionless.UI/releases/tag/v2.3.1)

## Questions? Let Us Know!

If you've got any questions about any of the release notes above, please don't hesitate to let us know by commenting below or submitting an issue to the respective GitHub repo, above.

Thanks for checking out our release notes.
