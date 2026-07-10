---
title: "Logging Feature Usages with Exceptionless"
---

# Logging Feature Usages with Exceptionless

![Exceptionless Feature Usage](/assets/img/news/feature-usage.png)The ability to log feature usages is one of the many [new... features... of Exceptionless 2.0](/its-go-time-exceptionless-2-0-launched/).

If you want to know when a button is being clicked, or what users are doing certain tasks, feature usage logging will help you track and visualize each occurrence.

What you learn from logging these types of feature usages might surprise you, and at the very least you'll know more about how users interact with your system.

## How to Submit a Feature Usage

### Using the client

For this example, we are assuming that you have already created an account and installed and [configured](/docs/clients/dotnet/configuration) the Exceptionless 2.0 client. If you are still using the 1.x client, you will need to [upgrade](/docs/clients/dotnet/upgrading) to send us feature usage events. Please contact support via an in-app support message or our [contact page](/contact/) if you have any questions or need assistance in this area.

#### Example 1 - Signup

In the example below, we are going to submit a feature usage when any user signs up.

**To submit the feature occurrence, you just need to call our api as follows:**

```cs
using Exceptionless;
ExceptionlessClient.Default.SubmitFeatureUsage("Signup");
```

SubmitFeatureUsage creates a simple feature usage and submits it. To include more information, please use CreateFeatureUsage (example below).

#### More detailed examples

You can also submit additional information with a feature usage using our fluent api. This is nice when you want to add contextual information, contact information, or a tag.

#### Example 2 - Signup with tags

If, for instance, you wanted to indicate how a user signs up, you can add a tag to the feature usage occurrence. In the below example, we are tagging a feature usage that uses GitHub to sign up.

```cs
using Exceptionless;
ExceptionlessClient.Default.CreateFeatureUsage("Signup").AddTags("GitHub").Submit();
```

#### Example 3 - Who's Searching?

As another example, maybe we want to log a feature usage when users search, and then set their identity.

```cs
using Exceptionless;
ExceptionlessClient.Default.CreateFeatureUsage("Searching").SetUserIdentity("John Smith");
```

#### What else can I add to a submission?

There are a number of additional pieces of data you can use for your event. The below bullets include the current EventBuilder list, but we are always adding more that can be found on [GitHub](https://github.com/exceptionless/Exceptionless.Net/blob/master/src/Exceptionless/EventBuilder.cs). Also, view more examples here on our [Sending Events](/docs/clients/dotnet/sending-events) page.

* AddTags
* SetProperty
* AddObject
* MarkAsCritical
* SetUserIdentity
* SetUserDescription
* SetVersion
* SetSource
* SetSessionId
* SetReferenceId
* SetMessage
* SetGeo
* SetValue
* And more, depending on your client

### Using the REST API

You can also submit a feature usage by posting JSON to our API. See details in our [API Documentation](https://api.exceptionless.io/docs/index.html#!/Event/Event_Post). If you need an API key for simply posting events, you can find it in your [project settings](https://be.exceptionless.io). Otherwise, please refer to the [auth login documentation](https://api.exceptionless.io/docs/index.html#!/Auth/Auth_Login) to get a user scoped api key.

```json
{
    "type": "usage",
    "source": "Signup"
}
```

## The Dashboard

Feature usage logging makes it very easy to see how often a feature, such as logging into an account, is used over time. If usage of one type of login, for instances, is never used, there might be an issue or you could consider removing it (less code debt). Or, if one is used the majority of the time, maybe it should be first in the list. There are almost unlimited ways you can use feature usage data to improve user experience.

![Exceptionless Feature Usage Dashboard](/assets/img/news/feature-usage-dashboard-1024x603.png)

If we click on the stack for the signup feature, it shows the tag list of the providers that have been used to signup (GitHub and Google, in this instance).

![Exceptionless Feature Usage Stack](/assets/img/news/feature-usage-stack-tags.png)

You can even go further and filter by a tag to see exactly how people are logging into your system using the search filter. Example: "tag:GitHub"

![Exceptionless Feature Usage Filtering Searching](/assets/img/news/feature-usage-tag-github.png)

Regarding the SetUserIdentity example above, we can see that user information being appended to a feature usage occurrence if we visit the "Search" feature and view an occurrence.

![Exceptionless Feature Usage User Info](/assets/img/news/feature-usage-searching-userID.png)

## Cool Stuff, Right?

We think so, and we hope you do too. Either way, we're always looking for feedback, so let us know what you think via the comments below, an in app support message, or via the [contact page](/contact/ "Contact Exceptionless").
