---
title: "Detailed Error Reports - What's Included?"
---

# Detailed Error Reports - What's Included?

When an error occurs in your app, you need to know the critical details, fast, so you can drill down and fix it. We get it - we're developers too - that's why we built Exceptionless.

The trick was organizing the data so it didn't overwhelm our users, while still providing all the important stuff so developers wouldn't have to spend extra time tracking down versions, requesting stack traces, or pulling teeth to get environment information.

Lets take a look at the **default** information included with every error. We say default because you can easily **add your own** information with custom objects.

## Each Error Occurrence

Every single error occurrence has the following tabs:

* Overview
* Exception
* Request
* Environment

On each of these, you can go to the previous or next occurrence for easy comparison.

Lets take a closer look at each tab.

### Error Overview

![Exception Reporting Overview](/assets/img/news/error-overview-tab-248x300.jpg)  

The overview tab holds general information for that occurrence, including the variables below. Sometimes this is all you'll need to track down the bug. Sometimes you'll need to dig deeper.

* Occurred On
* Error Type
* Message
* Platform
* URL
* Referrer
* Browser
* Browser OS
* User Name
* Stack Trace

### Exception Tab

![Exceptionless Exception Details](/assets/img/news/error-exception-tab.jpg)



  On the exception tab, we reference the timestamp, error type, message, and stack trace, while also providing you with all of the loaded modules, including versions.




### Request Tab


![Exceptionless Request Details](/assets/img/news/error-request-tab-225x300.jpg) 

Here we have tons of important request info:

* Occurred On
* HTTP Method
* URL
* Referrer
* Client IP
* User Agent
* Browser
* Browser OS
* Device
* Is Known Bot
* Cookie Values


### Environment Tab


![Exceptionless Environment Details](/assets/img/news/error-environment-tab.jpg)  

Environment isn't something that we always think about, but in some cases it can tell us a lot about the exception. We've got you covered!

* Occurred On
* Machine Name
* IP Address
* Processor Count
* Total Memory
* Available Memory
* Process Memory
* OS Name
* OS Version
* Architecture
* Runtime Version
* Process ID
* Process Name
* Command Line
* Client Information
  * Install Date
  * Version
  * Platform
  * Submission Method
  * Application Starts
  * Errors Submitted

## How Did We Do?

We have done our best to include all the important information in an organized, easy to read, intuitive interface. Think we're missing something? Think we can organize it differently? Let us know! We love feedback.
