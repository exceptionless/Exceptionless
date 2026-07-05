---
title: "Filter Improvements to the Exceptionless Single Page App"
---

# Filter Improvements to the Exceptionless Single Page App

![SPA filter search solutions](/assets/img/news/user-experience-search-filter-changes.png)It's been a while since we introduced filtering and searching when we launched Exceptionless V2.0, so we decided recently that we wanted to take the feedback we've received and do a round of improvements to it.

You may have already noticed the changes, but if not then the next time you log in you will see that the top bar has changed, giving you much quicker access to filtering and more upfront information.

## Filter Changes to the Desktop View

For the primary desktop view, we removed the magnifying glass icon in the top bar and simply filled the rest the bar with the filter (search) input box. This eliminates a click to get to the filtering system, and keeps it front and center at all times.

One **important note** here is that if you want to show events that have been marked as fixed or hidden, you have to explicitly specify those filters, whereas previously those options were check boxes. So, you can use `hidden:false` or `hidden:true`, and `fixed:true` or `fixed:false` to view those events. Naturally, the default is false for both, showing events that have not been hidden or fixed. This means that in order to see both hidden and un-hidden events, you would need to use `hidden:false OR hidden:true`. Likewise, for fixed, you would need to use `fixed:true OR fixed:false`.

You'll notice that the date/time filter has changed as well. Instead of an icon, we now display the current time frame being viewed, once again saving you a click and keeping things in front of you.

As always, the filter still applies to the chosen time frame only.

### Before

![old1](/assets/img/news/old1.png)

![old2](/assets/img/news/old2.png)

### After

![new7](/assets/img/news/new7.png)

![old3](/assets/img/news/old3.png)

## Mobile Changes & Functionality

We also changed the user interface for smaller screens.

Instead of the icons at the top of the screen, the time frame selector is now a major menu item in the mobile menu and displays the current selection with the filter/search field directly below it.

This setup should allow users to filter and find the events they seek much quicker on mobile.

### Before

![new2](/assets/img/news/new2.png)



### After

![new3](/assets/img/news/new3.png)

## Pretty Cool, Yeah?

We think it's a pretty nice improvement. We got feedback from several users and think making everything visible at the top level of the user interface is an important change that saves time and keeps you informed.

If you've got any additional feedback, please don't hesitate to let us know. We are always looking for ways to improve, and we use Exceptionless every day too, so we are always interested in saving ourselves time and making things easier on our end!
