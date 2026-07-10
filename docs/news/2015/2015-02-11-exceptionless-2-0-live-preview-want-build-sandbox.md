---
title: "Exceptionless 2.0 Live Preview! Do You Want to Build a Sandbox?"
---

# Exceptionless 2.0 Live Preview! Do You Want to Build a Sandbox?

![exceptionless-sandbox](/assets/img/news/exceptionless-sandbox.png)Oh boy! We're ready for you guys to beat on Exceptionless 2.0 in a sandbox environment!

We've made you wait, and everyone has done so quite patiently, but now it's officially time to check out V2.0 for yourself.

We're super excited, but we also 
**need your help!**
 At this point, we are asking for any and all 
**feedback** 
to help us tweak and refine and get things launched. Check out the details and instructions below, and make sure to send us thoughts, critiques, praise, and (of course) bug reports via [GitHub Issues](https://github.com/exceptionless/Exceptionless/issues/new).

## You Can Be.Exceptionless


![Exceptionless 2.0 Dashboard Preview](/assets/img/news/sandbox-preview-300x183.jpg)
_Exceptionless 2.0 Dashboard Preview_



**Simply...**

  1. Go to [https://be.exceptionless.io](https://be.exceptionless.io)
  2. Log in with your **existing** Exceptionless account
    _Some very recent accounts may not allow you to log in. If this is the case, please create new account for testing purposes._

      1. View your current data in the new system - play around with it!
      2. (Optional, but Encouraged) Upgrade your client and start sending events to Exceptionless 2.0!
        
**Notice:** All new data sent to this sandbox preview will be lost when the final version of Exceptionless 2.0 goes live. You will have a gap in historical data. Also, existing clients (1.x) will still work against the 2.0 API when we go live.


          1. Open the NuGet Package Manager dialog.
          2. Click on the Updates tab.
          3. Select "Include Prerelease" from the dropdown.
          4. Select the Exceptionless NuGet packages and click the Update button.
          5. **You should be good to go!
** If you need more info, view our [upgrading documentation](/docs/self-hosting/upgrading-self-hosted-instance) or contact us via in-app support.
  3. Provide feedback via [GitHub Issues](https://github.com/exceptionless/Exceptionless/issues/new). (good, bad, or ugly - we want it all)
  4. Be Exceptionless!

### Documentation

* [How to Upgrade 1.x client to 2.0](/docs/self-hosting/upgrading-self-hosted-instance)
* [Search Terms](/docs/filtering-and-searching)
* [Fully Documented API](https://api.exceptionless.io/docs/index.html)

### Notes


All events submitted to the 2.0 system may be reset at any time and will be reset before we go live (sandboxed). New events that are submitted, and any changes that happen in the 2.0 preview, will not be available in the 1.x system and will be lost once 2.0 goes live. Existing clients (1.x) will still work against the 2.0 API when we go live.


## What Exceptionless 2.0 Offers

We have covered many of these new features in previous update posts, but there are a few new additions and tweaks we've thrown in since then, along with links to previous discussions.

* **Searching / Filtering**
    We've implemented Elasticsearch and you can search/filter ALL the things! Read more [here](/making-move-elastic-search-exceptionless-2-0/) and watch a quick demo video [here](/filter-your-exceptions-video-demo/).

* **Cross Organization Views**
    You now have the ability to view all events across all organizations, a single organization, or a project.

* **PCL Support**
    We've built in client support for [portable class libraries](https://www.nuget.org/packages/exceptionless.portable)!

* ****New Clients!
**** Including: [Exceptionless.Portable](https://www.nuget.org/packages/exceptionless.portable) for console apps and [Exceptionless.NLog](http://www.nuget.org/packages/exceptionless.nlog), an nlog target that reports to Exceptionless

* **Fully Documented API**
    For all your API needs, check out the [API Documentation](https://api.exceptionless.io/docs/index.html)
* **Bulk Actions
** Select multiple events or instances of events and do with them as you please! [Watch the preview demo.](/bulk-actions-sneak-peak-exceptionless-2-0-video/)

* **Faster than Ever!
** Exceptionless 2.0 is a single page app (SPA) and is lightning fast. [We're using AngularJS](/angularjs-exceptionless-2-0/) and we're stoked to give our users a super quick experience!

* **And more...
** Check out more new features, including source links, in our [Exceptionless 2.0 Overview article](/upcoming-exceptionless-version-2-0-overview-review/). Includes details on: Event Based Reporting System, Simplified API, The Pluggable System, Client Rewrite, New Message Bus & Queuing, and Job System Enhancement.

## Help Us Make Exceptionless 2.0 Even Better

We've asked a few times already in this post, but we have to ask, once again, for your [feedback](https://github.com/exceptionless/Exceptionless/issues/new)on the V2.0 platform. Our users define Exceptionless, and to make it the absolute best we can, we rely on **you** to let us know what's good, what's bad, and what we shouldn't have even wasted the bytes on. So please, give it a go and use [GitHub Issues](https://github.com/exceptionless/Exceptionless/issues/new) to give us your thoughts. We appreciate it more than you know. Thanks!


