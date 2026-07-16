---
title: "Event Based Reporting System Coming in Version 2.0"
---

# Event Based Reporting System Coming in Version 2.0

[![Event Based Reporting System](/assets/img/news/errors-only.png)](/assets/img/news/errors-only.png)We [hinted](/exceptionless-2-in-the-making/)that more details on the upcoming Exceptionless 2.0 release would get announced soon, and here we are! Lets dive in a bit further, shall we?

Many users have asked for ways to use Exceptionless to report additional types of events, rather than just errors. With version 2.0, we are moving to an event based system that will accommodate such requests.

## What's an Event Based Real-Time Reporting Tool Look Like?

* The new system allows us to receive literally any data people want to send us instead of only allowing errors.
* Event posts can be as simple as this:
    [![Post Event Exceptionless](/assets/img/news/ex-curl.png)](/assets/img/news/ex-curl.png)
* You can send log messages or even entire log files.
* Log messages can contain extended data objects just like errors can now.
* You can post random JSON objects and the data within them will be treated as extended data.
* You can post batches of events instead of only being able to send one at a time.
* You can send feature usage events that let you see how often features of your application are being used. Think about how useful that will be!
* You can send session start and end events that will enable you to know what percentage of users are affected by errors and enable you to better know what your priorities should be.
* We will be gathering enough data to make it easy for us to begin putting together some **very** useful analytic reports.

We're pretty excited about the switch from error-only to send-us-any-event-you-can-think-of real-time reporting, logging, and notifications. We think it's going to be awesome, and it's almost scary how much of a playground Exceptionless is going to turn into for some of our customers. We're not pushing the limits, we're pushing for **no limits**!

Ideas? Concerns? Let us know. We're working hard to wrap up Exceptionless 2.0, but there's still a lot more bells and whistles we're polishing before launch! Keep an eye out for still more sneak peek material in the coming weeks!
