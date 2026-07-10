---
title: "XML Parsing Exception Caught and Fixed - Case Study"
date: 2014-02-07
---

# XML Parsing Exception Caught and Fixed - Case Study

This is yet another story of an elusive bug that, without a proper error reporting service, would have gone un-noticed for a very long time.

When third-party software and services interact with your code, in any way, you have to be wary that their data may be different from yours. In this instance, our client was parsing some XML that included an unfriendly cookie name. Lets see what happened next...

![.NET Exception Dashboard](/assets/img/news/apexExceptionsRandom.png)

## Killer Cookies

[Listrak](http://www.listrak.com/) is a pretty cool company that provides, among other things, a robust email marketing, shopping cart abandonment, and welcome series suite of tools.

One of their cookies uses a "$" in it's name ($ListrakFoo or similar). When the client's XML Config packages were executed, that $ was causing an XML parsing exception.

### That's not customer facing. What's the big deal?

#### 37,000

While not directly customer facing, it was happening on almost every page of the website and occurred something like 37,000 times in the span of it's existence.

#### Sluggish

Along with the sheer number of occurrences, it was also noticeably slowing the site down.

### The fix

The fix was relatively simple. The client now checks for "$" in cookie names within the affected method and just doesn't handle them.

**Tracking errors like this is what we're all about.** There are hundreds of thousands, probably even millions, of bugs out there _right now_ that throw exceptions every day but manage to remain hidden in the shadows of code written by professional developers.

It's not a sign of poor programming, it simply comes with the territory. You will have bugs, big and small, in every project you work on. The difference is, with Exceptionless, you can actually be aware of, and fix, them!

Another bug bites the dust, thanks to help of Exceptionless' [real-time exception reporting](https://exceptionless.com "Real Time Exception Reporting")!

Do you have a story where our software helped you track down and squash an elusive error? Let us know! We'll publish the details and link back to your app, if you like.
