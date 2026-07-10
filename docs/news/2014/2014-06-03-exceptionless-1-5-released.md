---
title: "Exceptionless 1.5 Released!"
date: 2014-06-03
---

# Exceptionless 1.5 Released!

![Exceptionless Version 1.5](/assets/img/news/version1.5.png)While we're [on the march to Exceptionless 2.0](/exceptionless-2-in-the-making/ "Exceptionless 2.0 – In the Making"), we're still making updates and fixing bugs on version 1. Today, we'd like to announce that Exceptionless 1.5 has been released, which includes several server changes and bug fixes, as well as major client code base optimization.

Please **update your client to version 1.5** and take a look at the other changes and bug fixes, below. We've done quite a bit of work to notifications, added throttling to improve coverage on small and free plans, and improved performance in a few places.

## Server Changes

* Added **throttling to accounts that are over their usage limits**. If an account is sending a high number of errors, the errors will be throttled on an hourly basis so that the entire plan limit won’t be used up immediately. This allows for a distributed sampling of the errors instead of only capturing everything in a short period of time.
* Added a site notification that shows you when error submissions are being throttled or if you are over your monthly plan limits.
* Removed total count from most recent errors list as it was a very expensive to calculate while providing little value.
* Fixed a bug with notifications that could cause some users to get spammed. Now notifications only send a maximum of 10 notifications per project every 30 minutes.
* **Greatly simplified the authentication logic** for the web api pipeline.
* Added the ability to **print all content on the error occurrence page**.
* The pager will no longer scroll to the top of the current list when changing pages.
* Updated the paged lists to only refresh the list data via push notifications when you are on the first page.
* The list data will only be updated in real time if the data matches the current filter criteria.
* Fixed a bug where the loading indicators would appear on the suspended and manage organization pages.
* Fixed a bug where the save button on the manage organization page would have improper styling.
* Fixed a bug where a HttpAntiForgeryException could be thrown when accessing the website.
* Fixed a bug where a ArgumentException would be thrown if multiple model validation errors occurred on a single page.
* Fixed a bug where a NullReferenceException could be thrown when signing up.
* Added some additional checks to try and resolve the user profile when an invited user signs up.
* Fixed a bug where an updated organization notification could be sent before the user was authorized to access the organization.
* Fixed a bug where empty OS Name and Version values were being shown in the errors environment section even if they didn't exist. This could happen if the client was reporting from an azure website instance.
* Changed billing plans to use per month error limits.
* Fixed a bug where the BillingManager could throw a NullReferenceException for a newly added organization. This could happen because the primary node had not replicated the content to the secondary nodes or the data wasn't cached on creation.
* Updated various MongoDB collections to not persist empty array fields.
* Fixed a bug where some cache entries were not automatically expiring.

## Client Changes

It's **highly recommend that you update your clients** to 1.5 as we did major optimizations to the client code base.

* Greatly simplified how the client processes and sends errors. The client now properly handles the various status codes that can be returned from the service.
* Added an event that allows you to customize the request object before it is sent to the service.

**As always, please let us know if you have any questions!**


