---
title: "Saved views"
description: "Save a list's filters, date range, sort, and columns as a named view, share it with your organization or keep it private, and choose your home view."
sources:
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Core/Models/SavedView.cs
  - src/Exceptionless.Web/Models/SavedView/NewSavedView.cs
  - src/Exceptionless.Web/Api/Handlers/SavedViewHandler.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/saved-view-picker.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/save-view-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/rename-view-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/delete-view-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/column-management-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/saved-view-order-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/use-saved-views.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/saved-view-drafts.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/defaults.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/ordering.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/slugs.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar.svelte
---

# Saved views

A saved view is a named list: its filters, date range, sort order, columns, and whether it shows the stat boxes and
chart. The **Stacks**, **Events**, and **Sessions** pages each show a saved view, and every view in the organization is
listed under its page in the sidebar. Save a view for anything you check often, such as production errors in the
checkout service, and share it with your team.

Each view owns its own filters. There's no global project or date filter: changing the filters on one view doesn't
change any other view.

## Built-in views

Every organization starts with these views. They cover the last 7 days, and all of them except **Events** > **All** and
**Sessions** > **All** show only open and regressed stacks.

| Page         | View                     | Shows                                                           |
| ------------ | ------------------------ | --------------------------------------------------------------- |
| **Stacks**   | **All**                  | Every open or regressed stack                                   |
| **Stacks**   | **Most Frequent Errors** | Error stacks, most events first                                 |
| **Stacks**   | **Most Frequent 404s**   | 404 stacks, most events first                                   |
| **Stacks**   | **Most Used Features**   | [Feature usage](/docs/feature-usage/) stacks, most events first |
| **Events**   | **All**                  | Every event, newest first                                       |
| **Events**   | **Errors**               | Error events, newest first                                      |
| **Events**   | **Logs**                 | Log events, newest first                                        |
| **Sessions** | **All**                  | Every [session](/docs/user-sessions/), newest first             |

The built-in views are ordinary shared views in your organization. Anyone in it can change, rename, or delete them. When
Exceptionless updates a built-in view, it leaves the ones you've changed alone.

## Create a view

1. Open a view and set it up the way you want: add filters, change the date range, and arrange columns.
2. Select **View** at the top right, then **Save As...**.
3. In **Save View**, enter a **Name**. The **URL name** fills in from it; change it if you like.
4. Check **Private** if only you should see it.
5. Select **Save**.

The view opens and appears in the sidebar under its page. Its address uses the URL name, such as
`/stack/production-errors`, `/event/production-errors`, or `/sessions/production-errors`.

If your filters already match a saved view, **Save View** says so and offers to load that view instead.

Names and URL names must be unique among the views on a page. A URL name can contain only lowercase letters, numbers,
and single dashes. An organization can have up to 100 saved views.

## Unsaved changes

When you change a view's filters, date range, sort, columns, or display settings, a dot appears on the **View** button.
Hover over it to see "This view has unsaved changes. Open to save or reset them." Then open **View** and select:

- **Save** to update the view. For a shared view, everyone sees the change.
- **Save As...** to keep the original and save your changes as a new view.
- **Reset to Saved** to discard your changes.

Unsaved changes stay with the view in your browser tab, so you can move between pages and come back to them. The address
bar includes your filter, date range, and sort changes, so a link you copy opens the view with them.

## Rename or delete a view

- **View** > **Rename** opens **Rename View**, where you can change the **Name** and **URL name**. Links that use the
  old URL name no longer open the view.
- **View** > **Delete "** _name_ **"** asks for confirmation, then deletes the view for everyone who can see it.

## Choose columns

Open **View** > **Manage Columns...** to open the **Column Picker**:

- **Selected Columns** are the columns in the table. Drag them, or use **Move up** and **Move down**, to reorder them.
  Check **Wrap** to wrap long text instead of cutting it off.
- Pick one column to fill the remaining table width, or **None** to keep every column at a fixed width.
- **Available Columns** lists the columns you can add. Search them and select one to add it.
- **Reset to default** restores the page's default columns.

You can also drag the edge of a column header to resize it. Column changes count as unsaved changes, so select **Save**
to keep them.

Under **View** > **Display**, turn **Stat boxes** and **Chart** on or off. These are saved with the view too.

## Share a view

A view is shared with everyone in your organization unless you check **Private** when you create it. Shared views appear
in everyone's sidebar, and anyone in the organization can save changes to them, rename them, or delete them. Private
views appear only in your sidebar, and only you can change them.

To send someone a view, copy the address from your browser. They need access to the organization to open it.

## Set your home view

When you open Exceptionless, it shows your home view. To choose it, open a view and select **View** > **Set as my home
view**. Select **Clear my home view** to undo it.

To set the home view for everyone in the organization, open a shared view and select **Set as organization home**. A
personal home view takes precedence over the organization home. With neither set, Exceptionless opens a **Stacks** view.

## Reorder views in the sidebar

Views are listed alphabetically until you reorder them. Drag a view up or down in the sidebar, or hover over **Stacks**,
**Events**, or **Sessions** and select **Reorder** _page_ **views** to arrange them in a dialog. The dialog shows which
views are **Private** and which are **Shared**, and **Reset to alphabetical** undoes your order.

The order is personal: it changes only your sidebar.

## Ask Exie

Exie can't create or change saved views, but it can answer the questions you'd build a view for:

```prompt exie
What are the most frequent 404s this week?
```

```prompt exie
Which errors are new in production in the last 24 hours?
```

```prompt exie
Which features were used most in the last 30 days?
```
