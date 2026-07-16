---
title: "Slack Integration Update & Recap"
---

# Slack Integration Update & Recap

![Exceptionless Slack Integration](/assets/img/news/slack-integration-ft-img-1024x538.jpg)

Since we first introduced Slack integration with the goal of further improving notifications in Exceptionless, we've come back around with updates, a few bug fixes, and wanted to give everyone a quick all-in-one overview of the feature!

Thanks to everyone that has provided feedback, bug reports, and suggestions. If _you_ have any, don't hesitate to [submit an issue over on GitHub](https://github.com/exceptionless/Exceptionless/issues). We're always looking to improve and would love your input!

Along the way, we have run into **incoming webhook issues**, some **usability/setup workflow updates** that needed to be made to make the process more usable, and, among other things, **incorrectly created action URLs** that weren't being handled correctly with rate limiting.

We'll cover **setting up Slack integration with Exceptionless** below, but you can also review the [original post](/news/2017/2017-06-05-exceptionless-slack-integration) and [weekly update video](https://youtu.be/U9GbYqWK1ik), the [first bug fix update](/news/2017/2017-06-20-slack-integration-updates-bug-fixes-weekly-update-5222017) and [video](https://youtu.be/WtHj9e4M9zU), and the most recent [Slack integration improvement update](/news/2017/2017-06-26-improvements-exceptionless-slack-integration) and [video](https://youtu.be/k4CMOk5lpVw).

## Setting up Slack Notifications for Exceptionless

First, go into your project's settings in the Exceptionless app and click on the Integrations tab.

![Exceptionless Slack setup](/assets/img/news/exceptionless-slack-setup.png)

Then, click on "Add Slack Notifications" and log in to your slack team.

Once logged in, you will need to select the channel you want Exceptionless Notifications to post to and click Authorize.

Once authorized, you can then configure the different notification settings by going back to the Exceptionless app and into the project's integrations tab again.

Once integrated and configured, notifications will look something like the below screenshot, with the message, type of event, stack trace, links to actions, etc.

We're excited to keep improving notifications, and would love for you guys to continue testing and providing feedback! What else would you like to see happening with notifications? What are we doing right, and wrong?

Integrations with other chat & productivity tools like HipChat and Microsoft Teams are on our list, as well, so if you use these please ping us and let us know so we can gauge interest!

Until next time, code on my friends.
