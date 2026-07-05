---
title: "Version 2.0's New Message Bus and Queueing Systems"
---

# Version 2.0's New Message Bus and Queueing Systems

In an effort to improve scalability, allow for new functionality to easily be added to Exceptionless, make the system less coupled, process things more efficiently, go fully Async, and further support Azure, we've been working hard on a new message bus and queueing system.

Lets take a look at a few of the details surrounding these new systems we're building for Exceptionless 2.0. Take a look and let us know what you think. If you've got questions or comments, we'd love to hear them!

## Message Bus

The new message bus allows us to publish and subscribe to messages across all our resources. We [subscribe to different types of messages to send SignalR notifications](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Web/Hubs/MessageBusBroker.cs) to the client.

## Queueing

The new queueing system allows us to enqueue expensive tasks that can be handled at a later time. This lets us greatly reduce the processing and latency times of the api.

* We stream event data that is posted to the event controller directly into the queue without taking the IO or Memory hit of processing it. This means that we can process more errors, faster, with less resources.

The system also supports retrying and discarding of data.

* We queue emails that need to be sent, as well as user defined webhooks that need to be called with data. Email servers on the sending and receiving can go offline or error out while sending, but by queuing the notification emails we can ensure you always get them by re-sending in the future, after a failure occurs. In the event that we can't send you an email after a few retries, we can discard the notification.

## More on Version 2.0

If you're just now learning about the upcoming Exceptionless 2.0, make sure to catch up on previous feature announcements and examples by reading the below articles.

* [Exceptionless 2.0 – In the Making](/exceptionless-2-in-the-making/ "Exceptionless 2.0 – In the Making")
* [Event Based Reporting System](/event-based-reporting-system-coming-version-2-0/ "Event Based Reporting System Coming in Version 2.0")
* [Simplified API](/upcoming-exceptionless-2-0-simplified-api/ "More from the Upcoming Exceptionless 2.0: Simplified API")
* [A Pluggable System](/coming-exceptionless-2-0-pluggable-system/ "Coming in Exceptionless 2.0 – A Pluggable System")
* [Exceptionless 2.0 Client Rewrite Sneak Peak and Example](/exceptionless-2-0-client-rewrite-sneak-peek-usage-example/ "Exceptionless 2.0 Client Rewrite Sneak Peek Usage Example")
