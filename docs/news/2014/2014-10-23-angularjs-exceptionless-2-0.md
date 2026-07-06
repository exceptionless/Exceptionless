---
title: "AngularJS and Exceptionless 2.0"
date: 2014-10-23
---

# AngularJS and Exceptionless 2.0

![Angular JS](/assets/img/news/AngularJS-large-300x84.png)As we plug away at Exceptionless 2.0, perfecting and future-proofing it, we wanted to stop and take the time to talk about AngularJS, how we are using it in version two, and what benefits we're going to get out of it.

Naturally, we want to maintain all current functionality while supporting planned V2.0 features like search and filtering. We also want it to be static and use the REST API for everything. We're pretty determined to not cheat on that point. Then, thinking ahead, building it to support easily adding new features in the future is a huge priority.

Lets look at these points in a bit more detail.

## Static UI

Using our fully documented REST API, the new Exceptionless 2.0 UI will be static, with no server side logic. What this allows us to do is host the UI on a content delivery network (CDN), guaranteeing faster load times for users anywhere in the world. **Everyone** loves fast load times!

## Much, Much Faster

Being a single page application (SPA), the new version of the app eliminates page loads. The only thing being loaded is the JSON data, and we can pre-load other content as users navigate the site. That, along with the static UI mentioned above, means we can deliver an app that responds almost instantaneously to the user's input. What a time to be alive.

## Maintainable Modularity

Version 1 taught us a lot. We gained valuable feedback from users, beat our head against several complex problems, and made countless wish lists for the future. Most of that lead to the desire for a more modular, easily maintainable system that new features could be added to with minimal reinvention.

AngularJS has helped us fulfill those dreams. We can now add new features very easily, without breaking other sections of the site, and our code based has been greatly simplified. And, even with all the modularity, we are still able to run tests on the UI. Pretty snazzy!

Event type support is handled via pluggable view modules, and we'll be able to add new ones that we haven't even conceived of with relative ease.

## Do You Angular?

Making the choice to go with AngularJS wasn't easy or quick, but be believe it was the right path to take.

Do you Angular? What apps have you used it on? If not, what other frameworks do you use for this type of app, and why?


