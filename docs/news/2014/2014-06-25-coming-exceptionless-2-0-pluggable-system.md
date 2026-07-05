---
title: "Coming in Exceptionless 2.0 - A Pluggable System"
---

# Coming in Exceptionless 2.0 - A Pluggable System

![Pluggable System](/assets/img/news/pluggable-system.jpg)

In the last Exceptionless 2.0 article, we announced the upcoming [simplified API](/upcoming-exceptionless-2-0-simplified-api/). Today, we want to introduce another major piece of V2.0 - the **pluggable system**.

Plugins will allow customization and translation throughout the Exceptionless platform, including integration with third-party services and more. Read on for more details about pluggable details such as event parsing, event pipeline, and formatting.

## Event Parsing

* Has access to the raw POST data as well as the content type and submission client info.
* Used to translate that raw data into events.
* Can easily create new plugins to support new data formats like system logs.
* Can be used to support other JSON formats like adding support for clients made for other systems.

[Source](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Plugins/EventParser/IEventParserPlugin.cs)

## Event Processor

* Can be used to add new functionality to the system.
* Gets called on startup, when an event is starting to be processed and when an event is done being processed.
* Has access to settings from both the org and project level.
* Can be used to create integrations for 3rd party services like HipChat, Trello, GitHub, Slack, etc.

[Source](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Plugins/EventProcessor/IEventProcessorPlugin.cs)

## Formatting

* Used to control how events are displayed in the system.
* Controls the summary view of an event.
* Controls the stack title.
* Controls what notification emails look like.
* Controls which view is used to display the details of an event.

[Source](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Plugins/Formatting/IFormattingPlugin.cs)

We believe building a pluggable exception reporting system and allowing third-party service and app access will create one of the most flexible, usable, and friendly solutions on the market.

### Coming Soon

We're anxious to get Exceptionless 2.0 wrapped up, but we do not have an ETA currently. We are working hard and making good progress, so keep an eye out for more sneak peeks, feature announcements, and progress reports!

As always, please let us know if you have any feedback or questions.
