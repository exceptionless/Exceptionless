---
title: "Find Customer Facing Bugs with Exceptionless - Case Study"
date: 2013-12-19
---

# Find Customer Facing Bugs with Exceptionless - Case Study

## The Power of .NET Error Reporting

![ApexCCTV Logo](/assets/img/news/header-logoNew.png) One of our long time customers, ApexCCTV, recently described how Exceptionless' .NET error reporting allowed them to find and squash a bug that was causing their website to be completely unusable by some customers.

Because of the e-commerce development environment being used, this issue would not have been brought to their attention without the implementation of Exceptionless, making this a prime example of how powerful the tool is and what it can mean for all types of .NET web applications.

### What customers were experiencing

> "After a site upgrade, **every page** of the site was crashing if the customer had been previously logged in." - ApexCCTV

So, if a user had selected the "keep me logged in" box the last time they logged in, prior to the update, nothing was working for them.

### The cause

When a customer logs in, a secure cookie for that user is created behind the scenes. The code update had changed the format of the cookie, but not the name, so when a user visited the site with the old cookie, the authentication was breaking. This was throwing errors and crashing the site for the customer, but because the development team clears cookies regularly, and no one had reported any issues, the exceptions were flying under the radar.

### The fix

Exceptionless logged and reported the error, and the fix was easy - simply change the cookie name so that users were forced to get a fresh cookie the next time they visited the site. Bam, done! Users could then happly log in and buy [security cameras](http://www.apexcctv.com) without any issues.

There could be, and a lot of the times are, errors like this in any .NET project, and using a real-time exception reporting tool like Exceptionless can really be an asset, saving time, money, and customers.
