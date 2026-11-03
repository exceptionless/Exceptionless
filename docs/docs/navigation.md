---
title: "Navigating the app"
description: "Find anything in Exceptionless with the command palette, move around with keyboard shortcuts, and learn the app with guided tours."
navTitle: "Navigation"
sources:
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/keyboard-shortcuts.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/keyboard-shortcuts-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/navigation-command.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/navbar.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar-user.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar-organization-switcher.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/project-command-actions.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/data-table/data-table-body.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/product-tours/catalog.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/product-tours/components/dialogs/product-tour-catalog-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/product-tours/components/alerts/product-tour-welcome.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/product-tours/components/alerts/product-tour-feature-announcement.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/product-tours/components/product-tour-shell-spotlight.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/product-tours/components/product-tour-spotlight.svelte
---

# Navigating the app

The Exceptionless app has a command palette for finding things and running actions, single-key shortcuts for the places
you go most, and guided tours that show you around.

## Command palette

Press <kbd>/</kbd>, or click **Type / to search** in the top bar, to open the command palette. Type to filter it, use
the arrow keys to pick an item, press <kbd>Enter</kbd> to open it, and press <kbd>Esc</kbd> to close the palette.

Type at least two characters to also search the current organization's events and stacks from the last 7 days. The
palette shows up to three of each, with **View all matching events** and **View all matching stacks** to open the full
results. It searches the same fields as the filter bar's text search, so you can paste an event ID, a
[reference ID](/docs/references-ids/), an error message, or a URL path.

The palette also lists:

- **Pages:** **All Stacks**, **All Events**, and **All Sessions**, each followed by your
  [saved views](/docs/saved-views/) for it; the Exie page; settings pages for your account, the current organization,
  and the current project when you're in one; and help links such as **Documentation** and **API**.
- **Project Actions:** **Open Project**, **Project Stacks**, **Project Events**, **Project API Keys**, **Project
  Webhooks & Integrations**, **Project Source Maps**, **Project Notifications**, **Client Setup**, **Generate Sample
  Data**, and **Reset Project Data**. Pick an action, then pick the project.
- **Guided Tours:** opens the tour list.
- **Exie:** **Ask Exie**, **Triage Recent Errors**, and **Analyze Error Trends**, when [Exie](/docs/ai/exie/) is
  available.
- **Organizations:** **Switch to** another organization, **Switch Organization**, **View Organization Users**, **Invite
  User**, and **Add Organization**.
- **Actions:** **Toggle Theme**, **Refresh Current View**, **Open User Menu**, **Keyboard Shortcuts**, **Log Out**, and
  **Chat with Support** where support chat is available.

Many items match related words too. For example, typing `members` finds **Users**, and `MCP` finds **AI Tools**.

## Keyboard shortcuts

Press <kbd>?</kbd> to see this list in the app.

| Key              | Action                                                                                   |
| ---------------- | ---------------------------------------------------------------------------------------- |
| <kbd>I</kbd>     | Go to **Stacks**                                                                         |
| <kbd>E</kbd>     | Go to **Events** (all events)                                                            |
| <kbd>/</kbd>     | Open the command palette                                                                 |
| <kbd>A</kbd>     | Open or close [Exie](/docs/ai/exie/), when Exie is available                             |
| <kbd>O</kbd>     | Open the organization switcher, or the organization's settings if you belong to only one |
| <kbd>U</kbd>     | Open the user menu                                                                       |
| <kbd>?</kbd>     | Show the keyboard shortcuts                                                              |
| <kbd>Enter</kbd> | Open the selected command, or the focused row in a list                                  |
| <kbd>Space</kbd> | Toggle the focused checkbox in a list                                                    |
| <kbd>Esc</kbd>   | Close the command palette, menus, and popovers                                           |

The single-key shortcuts don't fire while you're typing in a field, while you hold <kbd>Ctrl</kbd>, <kbd>Cmd</kbd>, or
<kbd>Alt</kbd>, or while the command palette, the shortcuts list, or the organization or user menu is open.

Other places where the keyboard helps:

- In Exie, <kbd>Enter</kbd> sends your message and <kbd>Shift</kbd>+<kbd>Enter</kbd> adds a new line.
- During a guided tour, <kbd>Esc</kbd> ends the tour, and <kbd>←</kbd> and <kbd>→</kbd> move between steps while a tour
  button has focus.

## Guided tours

Guided tours highlight parts of the app and explain them, one step at a time. To open the list, choose **Guided Tours**
in the command palette, or open the user menu and choose **Help** > **Guided Tours**.

| Tour                      | What it shows                                                                                  | Available when                         |
| ------------------------- | ---------------------------------------------------------------------------------------------- | -------------------------------------- |
| **Explore Exceptionless** | Stacks, events, the filter bar, saved views, Exie, and the command palette, in about a minute. | You have an organization.              |
| **Configure a project**   | Creating an organization and project, and connecting your app on the **Client Setup** page.    | Always.                                |
| **Create a saved view**   | Saving a set of filters as a view you can come back to.                                        | You have an organization.              |
| **Investigate an error**  | Opening an error, seeing its impact, and reading what happened.                                | Your projects have at least one error. |
| **Meet Exie**             | Opening Exie and asking your first question.                                                   | Exie is available to you.              |

Each tour shows **Start**, **Continue** if you left it partway through, or **Restart** if you've finished it. Finished
tours are marked **Completed**. If a tour isn't available yet, the list says why, such as **Send an error report before
starting this guide.**

When you first sign in, a **Welcome to Exceptionless** card suggests a tour until you start one or close the card:
**Continue setup** if you haven't connected an app yet, or **Explore Exceptionless** if you have. **Browse guides**
opens the full list.

## Ask Exie

[Exie](/docs/ai/exie/) is on Medium plans and up. These are the prompts behind the command palette's Exie commands:

```prompt exie
Triage the most important recent errors in the current context. Summarize their impact, likely causes, and the next investigation steps.
```

```prompt exie
Analyze error trends in the current context over the last 7 days. Highlight spikes, regressions, and the issues that deserve attention first.
```
