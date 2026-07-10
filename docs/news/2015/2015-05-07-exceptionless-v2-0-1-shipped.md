---
title: "Exceptionless V2.0.1 Shipped!"
---

# Exceptionless V2.0.1 Shipped!

This release focused on bug fixes since the 2.0 release and include the below notable changes.

## Exceptionless Server

* API Status page now also checks the status of Storage, Queues and Message Bus.
* **Added** the ability to requeue events (E.X., archived or events that failed to process).
* **Added** the ability to send out system and release notifications.
* Made the event posting and processing async. This has huge performance gains under load.
* The GeoIP Database is now stored in the storage folder. This made it easier to update it via a job as well as removed some extra configuration settings.
* Made some minor changes that make it a bit easier to self host (more to come 2.1).

Please take a look at the Exceptionless [changelog](https://github.com/exceptionless/Exceptionless/compare/v2.0.0...v2.0.1) for a full list of the changes.

## Exceptionless.UI

* **Added** a busy indicators to some buttons allowing you to see the state of an action (E.G., Marking a stack as fixed).
* **Added** the ability to refresh the app if there is a critical website bug.
* **Fixed** a bug where some stack traces couldn't be displayed.
* Made some minor changes that make it a bit easier to self host (more to come 2.1).

Please take a look at the Exceptionless.UI [changelog](https://github.com/exceptionless/Exceptionless.UI/compare/v2.0.0...v2.0.1) for a full list of the changes.

## Let us know if you have any questions!
