---
title: "Exceptionless Helps Identify Issue Affecting 25,000 Users - Case Study"
---

# Exceptionless Helps Identify Issue Affecting 25,000 Users - Case Study

![CEVO, Inc. Logo](/assets/img/news/cevo-logo-300x60.png)Case study time!

Today we've got a great example of Exceptionless helping software developers identify major issues in their web applications affecting thousands of users.

In this case, the problem was major enough that the development team stopped using WPF and rewrote their entire UI layer!

Here's what Will Graham, [CEVO, Inc.](http://cevo.com/) developer, had to say when we asked him a few Exceptionless case study questions.

## Exceptionless Case Study - CEVO, Inc.

### What’s the number one customer-facing bug Exceptionless helped you squash?

> "The issue was so prevalent we completely ditched WPF and rewrote our UI layer in GDI+/WinForms" - Will Graham, CEVO, Inc

**Will:**

Definitely crashes related to .NET Framework installation corruptions on end-user machines. While Exceptionless didn't (and can't) immediately point and say, "here's how to fix it", it did shine light on an issue that was affecting approximately 5% of our 500,000 installed base. The issue was so prevalent we completely ditched WPF and rewrote our UI layer in GDI+/WinForms, but **Exceptionless gave us insight to see the problem and how many users were being affected**.

### Were you surprised at the initial results of using Exceptionless for the first time? How many errors were you seeing?

> "Exceptionless has changed our internal development process and how we approach code. Proper error handling and visibility is now a first-class priority for us and Exceptionless makes it super easy." - Will Graham, CEVO, Inc

**Will:**

Yes and no. **Error handling and tracking had always been more of an afterthought** - like, something is wrong, we need to look into it now. **We weren't aware of issues until after our users reported it** and filtering out the noise to find the signal of real issues was a time consuming process. Running on the Azure stack, we were **frustrated with the information provided** in the default diagnostics logging.

**When we added Exceptionless, we knew we had problems** with our system, we just didn't know where and to what extent. To be honest, **Exceptionless has changed our internal development process** and how we approach code. Proper error handling and visibility is now a first-class priority for us and Exceptionless makes it super easy.

### What is the number one internal bug you were able to track down with Exceptionless?

**Will:**

The biggest bug would have to be intermittent failures on our Service Bus instance. Using the metrics provided by Exceptionless, we were able to spend time implementing fault tolerance in very focused areas of the code based on incidence rate and end-user impact.

### If you had one feature you’d like us to add to Exceptionless, what would it be?

**Will:**

I'm not a huge fan of the current dashboard. It's effective, but with the amount of data Exceptionless has at it's disposal, I feel there's more interesting (and actionable) information that can be shown.

## We Love It!

Thanks for your time, Will! We love to hear from our users, and we'll definitely take that dashboard feedback and see what we can do there to make it more useful.

Do you have feedback on Exceptionless? Has it helped you find bugs and beat them into submission? Join us in the [Exceptionless Discord](https://discord.gg/6HxgFCx) and let us know!
