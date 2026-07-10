---
title: "Find Leaking Exceptions by Eliminating Empty Catch Blocks - Case Study"
date: 2014-01-04
---

# Find Leaking Exceptions by Eliminating Empty Catch Blocks - Case Study

## How a client eliminated 50+ exceptions/hour

Recently, a client contacted us and gave us a pretty incredible case study. He found a huge number of "leaking" exceptions by eliminating all of the empty catch statements in his ASP.NET code and sending an exception to Exceptionless instead. By doing so, he was able to track down about **12 individual bugs** and get rid of **over 50 exceptions per hour** by fixing them. That's **1200+ exceptions per day** - gone!

> "This gave us a picture of just how many poorly written methods were leaking exceptions. The answer was, A LOT. So, even though these weren't customer facing, they were expensive and a good indicator of code quality. In just a couple of days, we were able to eliminate the majority of them completely." - Eric Burcham

After speaking with Mr. Burcham further, he explained that while there were only about 12 individual bugs, the number of times that those bugs were occurring in different areas throughout the code (an ASP.NET eCommerce solution) was numerous. Luckily, he was able to use several multi-line find and replace actions to make all the changes relatively quickly and get things back on track.

## A brief code example

### Empty .NET catch blocks

Before using Exceptionless to generated exceptions, the catch blocks were empty.

```cs
try
{
  // Do Something
}
catch { }
```

### Using Exceptionless to generate an exception in the catch block

By submitting an exception in the catch block, instead, their eyes were opened to thousands of daily exceptions.

```cs
try
{
  // Do Something
}
catch(Exception ex)
{
  ex.ToExceptionless().Submit();
}
```

### But I can already throw an exception in my catch block...

While you can throw an exception in a catch block normally with .NET, Eric was able to utilize Exceptionless' intelligent error grouping, notifications, and detailed reporting to pinpoint the problems and deal with them accordingly. Then, he was able to monitor the decline of occurrences and make sure that there were no regressions. That's pretty powerful, we think.
