---
title: "IT'S GO TIME - Exceptionless 2.0 Launched!"
---

# IT'S GO TIME - Exceptionless 2.0 Launched!

![version-2.0-launched](/assets/img/news/version-2.0-launched.png)Today, after much development and [weeks of live user testing and feedback](#and-more), we have officially released Exceptionless 2.0 into the wild!

Users will notice a completely new interface and experience **numerous new features and improvements**, highlighted below. Existing users that [update their clients](/docs/self-hosting/upgrading-self-hosted-instance) (not required) will experience improved client efficiency, the ability to send us logs, and more. We believe 2.0 will usher in a new era in event reporting and logging, becoming a true asset to developers everywhere.

Shipping 2.0 out to our amazing customers has us **overwhelmed with excitement**, and we can't wait to see all the new ways in which Exceptionless will be used to squash bugs and improve apps everywhere. Read on for more details on updating existing clients and all the new features and changes. **We know you'll love it.**



  [Check Out Exceptionless 2.0 Now!](https://be.exceptionless.io/)





## Updating Clients for Existing Users

Follow these quick and easy steps below to update your Exceptionless client to 2.0.

  1. Open the NuGet Package Manager dialog.
  2. Click on the Updates tab.
  3. Select the Exceptionless NuGet packages and click the Update button.
  4. **You should be good to go!
** If you need more info, view our [updating documentation](/docs/self-hosting/upgrading-self-hosted-instance) or contact us via in-app support. We're always here to help if you have any questions!

## Exceptionless 2.0 Feature Recap

### Check out these awesome new and improved features


![Exceptionless 2.0 Screenshot](/assets/img/news/version-2-launch-screenshot-150x150.jpg)

Exceptionless 2.0 is faster, sleeker, mobile-friendly, more functional, includes all the below major improvements, and has countless smaller tweaks and changes we poured our heart and soul into. It's a whole new system - check it out!

* **Searching / Filtering**
    We’ve implemented Elasticsearch and you can search/filter ALL the things! Read more [here](/making-move-elastic-search-exceptionless-2-0/) and watch a quick demo video [here](/filter-your-exceptions-video-demo/).

* **Cross Organization Views**
    You now have the ability to view all events across all organizations, a single organization, or a project.

* **PCL Support**
    We’ve built in client support for [portable class libraries](https://www.nuget.org/packages/exceptionless.portable)!

* ****New Clients!
**** Including: [Exceptionless.Portable](https://www.nuget.org/packages/exceptionless.portable) for console apps and [Exceptionless.NLog](http://www.nuget.org/packages/exceptionless.nlog), an nlog target that reports to Exceptionless

* **Fully Documented API**
    For all your API needs, check out the [API Documentation](https://api.exceptionless.io/docs/index.html)

* **Bulk Actions
** Select multiple events or instances of events and do with them as you please! [Watch the preview demo.](/news/2014/2014-12-12-bulk-actions-sneak-peak-exceptionless-2-0-video)

* **Faster than Ever!
** Exceptionless 2.0 is a single page app (SPA) and is lightning fast. [We’re using AngularJS](/news/2014/2014-10-23-angularjs-exceptionless-2-0) and we’re stoked to give our users a super quick experience!

### And more...

Check out more new features, including source links, in our [Exceptionless 2.0 Overview article](/news/2014/2014-08-13-upcoming-exceptionless-version-2-0-overview-review). Includes details on: Event Based Reporting System, Simplified API, The Pluggable System, Client Rewrite, New Message Bus & Queuing, and Job System Enhancement.

## Live User Testing Review & Notes

**Our live preview went great! Thank you EVERYONE that sent feedback and comments.**

We received some awesome feedback from many of our customers, made some UI/usability tweaks and improvements, and fixed a few minor bugs. We also added the ability to search custom fields, which is a pretty big deal for some.

Naturally, we used Exceptionless 2.0 to log, report on, and gather data for the preview - and it worked amazingly! (shameless, but true, promotion)

On average, we traced **200,000+ anonymous log messages** within the app **each day** from preview activity. That data allowed us to learn a lot more about the behavior of Exceptionless 2.0 in areas such as jobs and gave us additional insight into what was going on. We were able to use a combination of errors, logs, and feature usage metrics to track down and fix an issue with external logins, as well. Awesome!

The system also helped us track down and identify a performance issue that we were able to fix and improve.

Overall, we had no major surprises and were able to tweak and improve several pieces of the app that we think will make it even more awesome.

## Keep The Feedback Coming

No software application is ever "done," so make sure to keep the feedback coming. We've made a huge leap from Exceptionless 1.x, but we want to keep improving the system in all areas. **We love hearing from our users**, and respond to each email, in-app message, website form submission, etc. So, please, let us know what you think!
