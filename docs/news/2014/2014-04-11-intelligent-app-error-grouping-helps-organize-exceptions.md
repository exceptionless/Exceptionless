---
title: "Intelligent App Error Grouping Helps Organize Your Exceptions"
date: 2014-04-11
---

# Intelligent App Error Grouping Helps Organize Your Exceptions

![Exception Grouping Totals](/assets/img/news/thumbnail.png)Having a tool like Exceptionless to report and log your software's errors is great, but many of our clients experience thousands of instances of each error over various lengths of time, which can become overwhelming quickly.

We couldn't just leave them with a huge list of individual error occurrences to drudge through, so we went through several different potential options until we devised the best way to group them.

## Grouping Errors Intelligently

### What's there to group by?

  1. The details we provide on each error give us countless ways to group them. While some wouldn't make sense, we considered everything.

* Date
* Type
* Message
* Platform
* Location
* Browser information
* User information
* Environment information
* Request details
* and more...

### Drill down fast

Since we use Exceptionless to report and track down our own bugs, it was easy to put ourselves in our own shoes and think about what would allow us to drill down and fix errors quickly.

With that mindset, we decided that there were two important error details that should be used for grouping.

  1. **Where the error occurred**
    We felt that, first and foremost, we wanted to know where the error was occurring. Even though there is a possibility it might be occurring in multiple locations, we felt that each location represented its own importance in our grouping scheme.
  2. **Type of error**
    The type of error is also very important, and we felt that when you combine type with location, you get a set of errors that holds enough significant explicit data to be recognized as a group.

## What Grouping Allows Us to Do

When we group app errors by location and type, it allows us to report error instance counts, first occurrences, frequency of occurrence, and most recent occurrence on the dashboard.


[![Error Group Details](/assets/img/news/dashboard-home-150x150.png)](/assets/img/news/group-details.png)
_Error Group Details_








This seemingly basic grouping forms the basis for the different Exceptionless dashboard tabs and pages, thus becoming a major cornerstone for the platform. Click into a group, and you see the title, exception type, and location, along with a graph of occurrences and the most recent occurrences.

From there, you can drill down into each occurrence and scrutinize all of the [error's details](/whats-included-exceptionless-detailed-error-reports/ "Error Report Details").

That's how we do it! Any questions? Let us know.
