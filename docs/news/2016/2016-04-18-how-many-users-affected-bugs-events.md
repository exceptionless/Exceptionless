---
title: "Know How Many Users are Affected by Bugs and Events"
---

# Know How Many Users are Affected by Bugs and Events

**![exceptionless user tracking](/assets/img/news/users-featured-image.png)Prioritizing your bug fixes and development time** in general can be a daunting task.

Sometimes, as developers, **we want to work on this shiny widget** or this annoying bug, and we don't really have anything in our face telling us to **quit focusing on our dreams** and work on what matters to the bottom line.

I can hear you over there: **"But, my dreams are important!"** Well, yes, but you don't get to have fun working on those until you've made your **users** happy by fixing the bugs that are affecting the majority of them or expanding on that feature that they are all using every single time they use your app.

We've got something that will let you get those pesky tasks off your plate though, so you can move on to the fun stuff!



## Who and How Bad Is It?

Our new "users" column and "Most Users" dashboard lets you **know exactly what percentage of your users are being affected by events or using features**. This allows you to prioritize the most important bugs or features to work on right away and potentially backlog things that only a few users are having issues with or using.

Of course, you'll need to be sending at least a user id (and preferably a display name) for each user. We'll cover how to do that later in the article.

### Percentage of Users Column

The new Users column on your Exceptionless dashboard displays a percentage value for each event stack that represents the number of users that have been affected by the event or, if it is a feature, that have used the feature.

If you mouse over the percentage, you can see the number of users the percentage represents out of the total.

These numbers are dynamically calculated for your selected timeframe that you are currently viewing.

![users affected by bug](/assets/img/news/dashboardv2-edited-1024x662.png)

### Most Users Dashboard

Because the main dashboard shows you the most frequent events, not necessarily with the highest usage, **we thought it would be helpful to have a new dashboard that automatically sorts event stacks by the percentage of users affected,** letting you start at the top of an exception list, for example, and work your way down knowing you're always working on a bug, etc, that is affecting the highest percentage of users.

![user event dashboard](/assets/img/news/dashboard-most-usersv2-edited-1024x644.png)

## Setting User Identity

In order to assure you are getting value out of the user feature, you want to make sure you are setting the user. For Exceptionless to track a user for your app, you need to send a user identity with each event. To do so, you need to set the default user identity via the following client methods:

#### C#

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.SetUserIdentity("UNIQUE_ID_OR_EMAIL_ADDRESS", "Display Name");
```

#### JavaScript

```js
exceptionless.ExceptionlessClient.default.config.setUserIdentity('UNIQUE_ID_OR_EMAIL_ADDRESS', 'Display Name');
```

Once the user is set on the config object, it will be applied to all future events.

**Please Note:** In WinForms and WPF applications, a plugin will automatically set the default user to the `**Environment.UserName**` if the default user hasn't been already set. Likewise, if you are in a web environment, we will set the default user to the request principal's identity if the default user hasn't already been set.

### ASP.NET Example

You can also manually set the user info on the event directly. This is intended for **multi-user processes (web applications)[.](http://www.businessinsider.com/slack-free-unlimited-plan-has-limits-2015-6) **For most MVC and WebAPI packages, the user will be set automatically based on the logged in principal, so you don't have to do anything.

```cs
// Import the exceptionless namespace.
using Exceptionless;
ExceptionlessClient.Default.CreateFeatureUsage("MyFeature").SetUserIdentity("123456789", "Blake Niemyjski").Submit();
```

### JavaScript Example

If you're using the JavaScript client, the entire session of the client will typically be for a single user, so you should be able to set it one time when they log in to your app.

```js
exceptionless.ExceptionlessClient.default.setUserIdentity("id", "friendly name")`
```

Like with .NET, if you are running a multi-user process (Node.js), you'll need to set the user at the event level.

```js
// javascript
var client = exceptionless.ExceptionlessClient.default;

// Node.Js
// var client = require('exceptionless').ExceptionlessClient.default;

client.createFeatureUsage('MyFeature’).setUserIdentity('123456789', 'Blake Niemyjski').submit();
```

## How Will You Use this Data

We are always interested in how our users use our features, and if our users feature helps our users help their users, well, that's a win win for our users and their users. Go users!

Don't forget to stop by and let us know if you love or hate it, and of course let us know if you think we can improve on anything within Exceptionless.
