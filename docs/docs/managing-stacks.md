---
title: "Managing Stacks"
description: "Manage Exceptionless stacks: view stack details, mark them Open, Fixed, Snoozed, Ignored, or Discarded, flag future occurrences critical, and add links."
---

# Managing Stacks

Perhaps the most important thing you'll do in Exceptionless is manage your stacks. Think of stacks as your todo list for error management. And like any good todo list, you can drill into a task for more details, mark the task complete, or set the task aside for a later date.

But what is a stack, exactly? It's pretty simple, actually. A stack is an automatically ([or manually](/docs/manual-stacking)) grouped list of events. Events can be errors, messages, or logs. Exceptionless will try to automatically group the same events together while gathering as much information about them as possible (i.e. number of users affected, frequency of the event, last occurrence).

### Viewing Stacks

Now, let's take a look at how you manage these stacks. When you sign in, you land on the Stacks list. The **Stacks** menu in the sidebar has saved views for common lists: **All**, **Most Frequent 404s**, **Most Frequent Errors**, and **Most Used Features**. The Most Frequent views sort stacks by how many events they received.

![Most Frequent Errors view of the Stacks list](/assets/img/docs/managing-stacks-list-light.webp)

Click on a stack you'd like to manage (We recommend you start with the most frequent event because that's probably pretty annoying for your customers 😉). Clicking on a stack opens its details in a side panel, and the panel's **Open in new window** button opens the full stack page. The details include important information such as how many times the event occurred, the percentage of users affected, and the first and last occurrences of the event. Below the stack summary, you can page through the individual events in the stack.

![Stack details showing total events, users affected, and first and last occurrence](/assets/img/docs/managing-stacks-details-light.webp)

### Applying Statuses

To apply a status on a stack, click the status button in the top-right corner of the stack details (it shows the current status, such as **Open**) and pick a new status. The options are:

* Open
* Fixed
* Snoozed (choose 6 Hours, 1 Day, 1 Week, or 1 Month)
* Ignored
* Discarded

![Stack status menu with Open, Fixed, Snoozed, Ignored, and Discarded](/assets/img/docs/managing-stacks-status-menu-light.webp)

Marking the status of a stack will control your [filtering](/docs/filtering-and-searching) options, but it can also drive your workflow. Ignored stacks may be stacks you have no plans of addressing. Discarded stacks (which do not count against your plan quota) may be stacks that you can't easily prevent from coming through but you don't want to see and don't care about. Snoozing stacks allows you to be reminded at a future point to address the stack in question.

Let's take a look at each status in detail:

**Open** - This status indicates that the error is new or actively occurring. You will receive email notifications for these issues.

**Fixed** - Well, this one means you fixed it. If you look at Exceptionless as your todo list for errors, marking a stack as "Fixed" is like checking an item off your todo list. To be more specific, you won't get notifications if you have marked events as fixed. When you choose Fixed, you can optionally enter the [version](/docs/versioning) of your software in which the fix was introduced. If another error comes through of the same kind and it matches the version number that was supposed to fix the issue, your stacks will be marked as "Regressed" instead of "Fixed". Additionally, if you mark a stack as Fixed but do not supply a version number in which it was fixed, should the event happen again, the stack will be marked as "Regressed".

**Snoozed** - Marking a stack as snoozed hides it from your stack lists and means you will not get email notifications for whatever period of time you indicate. This is like your alarm clock in the morning. You know you need to take action (like wake up or fix the bug), but you really don't want to yet. Snooze has your back. After the specified period of time, the stacks will once again start alerting you.

**Ignored** - When you ignore a stack, you will no longer receive email notifications unless you change the status. You may choose to do this when you have no intentions of resolving the issue but still want to collect information from the stacks.

**Discarded** - You should use this status when you do not care about the event at all. If "Ignored" is turning your back on an event and trying not to see it, "Discarded" is putting its feet in cement and dropping it into the middle of a lake. And since these events are sleeping with the fishes, they don't count against your plan quota. You'll be asked to confirm before a stack is discarded.

Beyond statuses, you have options to further control your stacks.

### Additional Options

Click the gear button next to the status button to open the **Stack Options** menu.

![Stack Options menu with Future Occurrences Are Critical, Promote To External, Add Reference Link, View Stacking Information, and Delete](/assets/img/docs/managing-stacks-options-menu-light.webp)

Marking a stack with "Future Occurrences Are Critical" will automatically tag all subsequent events that come into that stack with the critical tag and will make them more prominent for you to review.

The "Promote To External" option allows you to send the stack details to an external source that you've configured as a promoted webhook in the [Integrations](/docs/integrations) section. If the project has no promoted webhook yet, Exceptionless offers to take you to the project's integrations. This can help you automate issue tracking and project management. For example, you can use this functionality to connect to [Zapier](https://zapier.com), a no-code automation tool. By doing so, you can automatically funnel events from Exceptionless to issues on Github or Jira. You can send text alerts to your devops team (just be smart about this one because those folks need sleep too!), or you can use the data from Exceptionless to build custom dashboards for your entire organization.

"Add Reference Link" allows you to attach a link to an external source. This is helpful when a customer files a GitHub issue, for example. You can link to GitHub or to a Slack conversation, or anywhere, really. Reference links are listed in the stack details.

"View Stacking Information" shows the values Exceptionless used to group events into the stack. This is handy when you want to understand why two events did or didn't land in the same stack.

"Delete" removes the stack and its events. You'll be asked to confirm first.

Managing your stacks is the core focus within Exceptionless, and we try to automate as much as possible for you. However, it is possible to manually stack events, allowing you some of the same management tools outlined here.

---

[Next > Manual Stacking](/docs/manual-stacking)
