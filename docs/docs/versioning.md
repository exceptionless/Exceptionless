---
title: "Versioning"
metaTitle: "Versioning and regression detection"
description: "Set an application version in Exceptionless so stacks marked fixed in a version reopen as regressed only when a newer version hits the same error."
---

# Versioning

Stacks and their fixed version are shared across [environments](/docs/environments/). Continue sending application versions so older deployments can report occurrences without incorrectly reopening a stack fixed in a newer version.

You can mark error stacks fixed and they won't show up or notify you until they regress!

## How does this work?

When an event comes into Exceptionless, we figure out what makes it unique and place it into a stack of similar events. This means that an error comes in, it will be placed in a stack of similar error events. All events may contain an application version, this can be used to track what versions of an application are causing errors or being actively used.

## How do I specify an application version?

An application version will try and be resolved if possible, but it's a good idea to specify a version if you can. Please view the client specific documentation below to learn more about setting an app version.

## Choose your Client

* [.NET](/docs/clients/dotnet/)
* [JavaScript / Node.js](/docs/clients/javascript/client-configuration)

## What does mark fixed do?

To mark a stack fixed, open the stack, click the status button in the top-right corner (it shows the current status, such as **Open**), and choose **Fixed**. You can also select several stacks in a list and choose **Mark Fixed** from the [**Actions**](/docs/bulk-actions) menu. Enter the version that contains the fix in the **Version** box, or leave it empty, and then click **Mark Stack Fixed**.

![Mark Stack As Fixed dialog with an optional version](/assets/img/docs/versioning-mark-fixed-light.webp)

When you mark a stack as fixed, the following meta data is recorded on the stack:

* when the stack was fixed
* and **optionally** the semantic version that you fixed this behavior in.

> **NOTE:** Versions are semantic versions. If you enter a **four-part version number** (E.G., `1.2.3.4`), the dialog changes it to `1.2.3-4` for you, and a two-part version like `1.2` becomes `1.2.0`. We automatically handle four-part versions when processing events.

Next, the stack is hidden from the default stack lists, which only show open and regressed stacks. To see fixed stacks, include **Fixed** in the list's **Status** filter.

This meta data is then inspected when an matching event is processed to determine if the stack and all occurrences should be regressed (_marked not fixed_). There are two scenarios where a stack will be marked as regressed:

1. If no fixed in version is specified, any occurrence with a date newer than the date the stack was marked as fixed.
2. If a fixed in version is specified, any occurrence that has a newer version specified (Example: `1.0.0` > `1.0.0-beta`).

---

[Next > Reference Ids](/docs/references-ids)
