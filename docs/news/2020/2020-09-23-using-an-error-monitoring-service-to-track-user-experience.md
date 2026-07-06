---
title: "Using an Error Monitoring Service to Track User Experience"
date: 2020-09-23
---

# Using an Error Monitoring Service to Track User Experience

In development, we tend to think of errors as things that are thrown when our code does not execute properly. Errors can be caught and handled or they can be missed and result in uncaught exceptions. But how do we classify errors that are not directly caused by the code we write? How do we identify and address errors that are caused by the design decisions we made (or didn't make)?

You may already be using an error monitoring service, and if you are, you could continue using that and reach for yet another tool to help you with your user experience woes. Or, rather than adding another product to your endless list of tools that keep your application running, you can use the error monitoring service to both monitor for traditional errors and user experience problems.

Let's take a look at how we can do this. There are plenty of logging services out there, and most of them do the same thing. However, we're going to take a look at [Exceptionless](/). Exceptionless is an especially attractive choice for three reasons:

  1. Fair pricing on their hosted version
  2. It's [open-source](https://github.com/exceptionless/Exceptionless) and can be totally self-hosted
  3. The API allows us to do exactly what I'm proposing in this article.

While self-hosting may be an attractive option (and one that I will surely write about ina future post), we're going to sign up for a free account using Exceptionless's hosted platform. To do so, go to [https://exceptionless.com](/) and click the Sign Up button in the top-right:


![Exceptionless home page](/assets/img/news/exceptionless_site.jpeg)


Once you sign up, you'll be prompted to name your team and project. You'll also need to choose the language in which your project is written. I'm choosing NodeJS, but you can choose whatever language applies to you because I'll be referencing cURL commands to keep our solution as general-purpose and adaptable as possible. Once you've created a project, you will be provided an API Key. Hold on to that, we'll need to use it as a bearer token later.

*Pro-tip: To convert a cURL command to the language of your choice, use [Postman](https://www.postman.com/) and import the raw command. You can then choose the code option and see how to run the API call in the language you prefer.

You'll want to follow the documentation to set up your codebase to send errors appropriately to Exceptionless, but we will also need to think about how we are going to handle these UX errors.

To do this, let's first think about some of the problems a user may face on a site and how we can handle them. A simple example that I can think of is what I'm going to call "Happy Path Slippage." We build applications with a happy path in mind. It's how we test, naturally. We have to force ourselves to test outside of the happy path, so it's also important to monitor how often our users deviate from the happy path.

Let's say we have a simple e-commerce application. The happy path, in this case, would be:

  1. User signs up
  2. User searches for a product
  3. User adds product to shopping cart
  4. User checks out

That is the ideal flow, but we know users won't always follow that flow. However, what we don't know is how often users will deviate and if they do deviate. To track this with Exceptionless, we are going to use simple GET requests with a query parameter to build a funnel analysis. We will want to track product searches, shopping cart adds, and checkouts.

Let's start with the setup for product searches. Remember, we're going to use a GET request. You can read more of the Exceptionless documentation [here](https://api.exceptionless.io/docs/index.html), but the request is pretty simple. We will want to pass in an indicator that the event is a `productSearch` and what the product is. We can do that like this:

```bash
curl --location --request GET 'https://api.exceptionless.io/api/v2/events/submit/usage?source=productSearch&message=YOUR_PRODUCT' \
--header 'Authorization: Bearer YOUR_API_KEY'
```

Feel free to add whatever product name you'd like in the query. Just replace `YOUR_PRODUCT` with the name of the product you'd like to track. You can run the cURL command from the command line or you can use it to build a real request you would use in your app. If we run that then take a look at our dashboard in Exceptionless, we can start to make use of the data.

The Exceptionless dashboard takes you to a handy chart of most frequent exceptions/errors. However, we're tracking User Experience issues tied to features in our application, so those events won't appear on the Exceptions dashboard. Instead, if you click the Features Usage link on the left navigation, then click Events, you should see your new `productSearch` event.


![Features Usage Dashboard Example](/assets/img/news/dashboard_empty.png)


Pretty cool! This alone starts to become useful. We can cut out a separate analytics tracking tool by using our error monitoring service (Exceptionless in this case) to track events outside the normal error reporting. But we can take it a step further.

Remember, we want to track the funnel from search to checkout. So, let's send through another event representing a `cartAdd` when a user adds a product to their shopping cart. Here we are adding an extra parameter to also track how many of the product is added to the cart.

```bash
curl --location --request GET 'https://api.exceptionless.io/api/v2/events/submit/usage?source=cartAdd&value=QUANTITY_ADDED&message=YOUR_PRODUCT' \
--header 'Authorization: Bearer YOUR_API_KEY'
```

Exceptionless has real-time monitoring, so if you flip back to your dashboard after running the above command, you should already see the event in the list:


![Cart Add Example](/assets/img/news/dashboard_evenets.png)


I think you're already seeing how easy this is, but let's round this out by adding a `checkout` event to track.

```bash
curl --location --request GET 'https://api.exceptionless.io/api/v2/events/submit/usage?source=checkout&message=YOUR_PRODUCT' \
--header 'Authorization: Bearer YOUR_API_KEY'
```

Again, your Exceptionless dashboard should update in real-time. This is starting to shape up! Now, let's dive into the events because right now we have the start of a nice funnel analysis, but we don't know yet what products were searched for, added to the cart, and checked out. The cool thing here is we can click into an event like `productSearch` for example and get detailed info.

Go ahead and try it. Click on the event and you'll be taken to a dedicated Event Occurrence page.


![Event Occurrence Example](/assets/img/news/event_occurence.png)


This is useful information. Combined with our user experience funnel analysis, we can start to make product decisions. Just for fun, I want to show you what this could look like when leveraging the Most Frequent view.

Again, we should click on the Features Usage link on the side navigation. This time, we'll choose the Most Frequent option. I've created a bunch of events so that we can see how useful the Most Frequent view can be.


![Funnel Example](/assets/img/news/funnel_example.png)


Now we have the makings of a useful way of tracking the user experience right from within our error reporting tool. The benefit here is that we can use a single tool to help us with monitoring, bugs, event tracking, and user experience. [Exceptionless](/) makes this incredibly easy, is self-hostable, is open source, and if you choose the hosted option is very affordable.

Go forth and track errors AND user experience all in one place.
