---
title: "Filtering and searching"
metaTitle: "Filtering and searching events in Exceptionless"
description: "Narrow any Exceptionless list with the filter bar: field filters, environments, raw filter queries, and the date range."
sources:
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/faceted-filter/faceted-filter-builder.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/faceted-filter/faceted-filter-keyword.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/faceted-filter/faceted-filter-actions.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/organization-defaults-faceted-filter-builder.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/models.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/environment-faceted-filter.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/date-faceted-filter.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/date-range-picker/date-range-picker.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/events-dashboard-chart.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/overview.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/premium-filter.ts
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
---

# Filtering and searching

Every list in Exceptionless, such as **Stacks**, **Events**, and **Sessions**, has a filter bar. Use it to narrow the
list by project, status, type, environment, tag, or any other field, and to set the time range. The filters belong to
the [saved view](/docs/saved-views/) you're looking at; there's no global filter that follows you between pages.

![Stacks list filtered by project, tag, and date range](/assets/img/docs/filtering-and-searching-filtered-view-light.webp)

The filter bar sits next to the view's name. Each filter is a button that shows its field and value. Changes apply right
away, and the address bar updates so you can share a link to exactly what you see.

## Add a filter

1. Select **Manage filters** (the **Filter** button when the view has no filters yet).
2. Type to find a field, then select it.
3. Pick or enter a value. Text filters apply when you press <kbd>Enter</kbd>; <kbd>Escape</kbd> cancels.

![Add filter menu listing the fields you can filter by](/assets/img/docs/filtering-and-searching-add-filter-light.webp)

The most common filters are **Project**, **Status**, **Type**, **Environment**, **Tag**, **Log Level**, **Message**,
**User**, and **Version**. You can also filter by **Source**, **Error Type**, **Error Code**, **Browser**, **OS**,
**Device**, **Machine Name**, **Ip Address**, **Http Path**, **Country**, **Reference**, **Session**, **First
Occurrence**, **Is Bot**, **Value**, **Count**, and more.

Selecting more than one value in a filter, such as two statuses, matches either value. Different filters all have to
match.

A text, number, date, or version filter with **No Value** matches items where that field isn't set. A **Project** filter
with no value includes every project.

To change a filter, select its button. At the bottom of each filter:

- **Clear value** empties it.
- **Hide filter** keeps it in effect but hides its button. **Show filter** brings it back.
- **Remove filter** removes it.

## Filter from an event or stack

You don't have to start from the filter bar. In an event's or stack's details, select the filter icon next to a value,
such as its type, source, version, environment, or user, to filter the list by it. Select a tag to filter by that tag,
and select **Open events for this stack** on a stack to list its events. See [Events](/docs/events/).

## Hidden filters

Views can include filters you don't see. The built-in views use them to keep the list focused: **Stacks** > **All** has
a hidden **Status** filter for open and regressed stacks, and **Events** > **Errors** hides its **Type** and **Status**
filters.

The number on **Manage filters** is the count of hidden filters. Open it and select the eye icon to show them, or the
eraser icon (**Clear all filters**) to remove every filter except the date range.

Selecting a field in **Manage filters** that the view already filters on, such as **Status**, opens that filter instead
of adding a second one, even when it's hidden.

## Filter by environment

Select **Manage filters** > **Environment** and pick one or more names, such as `production` or `staging`. The list
shows the environments seen in the selected projects and time range. To filter by a name that hasn't sent events yet,
type it and select **Use** _name_.

- **Unspecified** matches events that have no environment.
- With nothing selected, the filter reads **All environments**.

Environment names are matched without regard to case. See [Environments](/docs/environments/) for how events get an
environment.

## Search with a raw filter

A **Raw Filter** takes a query in the [search syntax](/docs/reference/search-syntax/), so you can use `OR`, exclusions,
ranges, wildcards, and custom data fields:

```text
(tag:Billing OR tag:Checkout) -bot:true
```

Open **Manage filters** and select **Raw Filter**, or type your query in the menu's search box and select **Create Raw
Filter**. Press <kbd>Enter</kbd> to apply it or <kbd>Escape</kbd> to cancel. The help icon in the filter opens this
page.

![Raw filter with a query using OR and an exclusion](/assets/img/docs/filtering-and-searching-raw-filter-light.webp)

A raw filter is combined with the view's other filters, including hidden ones, and with the date range.

Some examples:

| Query                                   | Finds                                            |
| --------------------------------------- | ------------------------------------------------ |
| `error.type:NullReferenceException`     | Errors of one exception type                     |
| `type:log (level:error OR level:fatal)` | Error and fatal log messages                     |
| `user:jane@example.com`                 | Events from one user                             |
| `version:2.4 environment:production`    | Events from any `2.4.x` version in production    |
| `_missing_:environment`                 | Events with no environment                       |
| `data.orderid:12345`                    | Events whose custom data has that order ID       |
| `count:>5`                              | Events that represent more than five occurrences |

See the [search syntax reference](/docs/reference/search-syntax/) for every field and operator.

> [!NOTE]
> On the Free plan, filters can use only a few fields, such as type, environment, status, and project. Other fields,
> search terms without a field, and custom data need a paid plan. See
> [Free plan searches](/docs/reference/search-syntax/#free-plan-searches).

## Set the date range

The **Date** filter sets the time range of the list. Select it, then pick a range:

- **Last 15 minutes**, **Last 1 hour**, **Last 4 hours**, **Last 24 hours**, **Last 7 days**, **Last 30 days**, or
  **Last 90 days**.
- **Custom range** for anything else.

A custom range has two tabs:

- **Absolute**: Set **From** and **To** with the date and time fields, or select days on the calendar. The first click
  selects a whole day and a second click extends the range. Times are in your browser's local time zone.
- **Relative**: Type expressions such as `now-1h`, `now-7d`, or `now/d` (the start of today). A year or month, such as
  `2024` or `2024-01`, covers all of it. Each field shows the date it resolves to.

Select **Apply** or press <kbd>Enter</kbd> to use the custom range.

On lists with a chart, drag across the chart to zoom into that time range.

The built-in views start at **Last 7 days**. Removing the **Date** filter shows all time, back to your plan's retention
limit.

## Save your filters

Changing a filter changes only the view you're on, and a dot on the **View** button shows the view has unsaved changes.
Open **View** to save them to the view, save them as a new view, or reset. See [Saved views](/docs/saved-views/).

## Search from anywhere

Press <kbd>/</kbd> to open the command palette and type at least two characters to search recent events and stacks. See
[Navigating the app](/docs/navigation/).

## Ask Exie

Exie filters with the same fields, so you can ask in plain words instead of building a query:

```prompt exie
Which errors did users on Chrome hit in production over the last 24 hours?
```

```prompt exie
Count the events for version 2.4.0 by environment for the last 7 days.
```

```prompt exie
Which stacks tagged Billing are still open?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can run the same searches:

```prompt agent
Use Exceptionless to find the errors with error.type NullReferenceException in production over the last 7 days,
then find where they're thrown in this repository and explain the likely cause.
```
