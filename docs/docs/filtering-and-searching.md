---
title: "Filtering & Searching"
metaTitle: "Filtering and searching events in Exceptionless"
description: "Search and filter Exceptionless events by fields like tag, type, level, version, or user, with AND/OR queries, wildcards, exclusions, and date ranges."
---

# Filtering & Searching

- [Views](#views)
- [Filter by Time Frame](#filter-by-time-frame)
- [Filter by Environment](#filter-by-environment)
- [Add Filters](#add-filters)
- [Search with a Raw Filter](#search-with-a-raw-filter)
- [Save a View](#save-a-view)
- [Searchable Fields & Requirements](#searchable-fields--requirements)
- [Multiple Queries](#multiple-queries)
- [Wild Cards](#wild-cards)
- [Exclusions](#exclusions)
- [Set and Unset Fields](#set-and-unset-fields)
- [Ranges](#ranges)
- [Custom Extended Data](#custom-extended-data)

## Views

Every list in Exceptionless is a view, and each view owns its filters. There is no global project or time filter. Pick a view from the sidebar under **Stacks**, **Events**, or **Sessions**, for example **Stacks > All**, **Stacks > Most Frequent Errors**, or **Events > Errors**.

The filter bar at the top of the view shows the view name, the date range, and any filters that are applied. Changing a filter only changes the view you are looking at.

![Stacks view filtered by project, tag, and date range](/assets/img/docs/filtering-and-searching-filtered-view-light.webp)

## Filter by Time Frame

Click the **Date** filter to change the time frame. Pick a preset, such as **Last 24 hours** or **Last 7 days**, or expand **Custom range** and enter a start and end. Custom values accept a year (`2024`), month (`2024-01`), day (`2024-01-01`), a timestamp, or a relative time such as `now-1h`. Click **Apply** to use the custom range.

You can also drag across the chart to zoom into a time range.

![Date range picker with presets and a custom range](/assets/img/docs/filtering-and-searching-date-range-light.webp)

## Filter by Environment

Choose **Manage filters → Environment** to select production, staging, development, or a custom name. Select **Unspecified** for events without this property, or clear the selection to include all environments. See [Environments](/docs/environments/) for client configuration and shared stack behavior.

## Add Filters

Click the **+** button next to the filters (it reads **Filter** when the view has none) to add a filter. Type in the search box to find a field, then pick it. Common choices are **Project**, **Status**, **Tag**, **Type**, **Log Level**, **Message**, **User**, and **Version**, and you can filter on many more fields, such as browser, OS, machine name, IP address, location, and error type.

Each filter shows up as a button in the filter bar. Click it to change its value. The buttons at the bottom of a filter let you clear the value, hide the filter, or remove it.

![Add filter menu listing the fields you can filter by](/assets/img/docs/filtering-and-searching-add-filter-light.webp)

Some views include hidden filters. For example, **Stacks > All** has a hidden **Status** filter that only shows open and regressed stacks. The number on the **+** button is the count of hidden filters. Open the menu and click the eye icon to show them, or the eraser icon to clear all filters.

You can also add filters from an event or stack. Click the filter icon next to a value, such as the type, source, or version, or click a tag, to filter the view by it.

## Search with a Raw Filter

To search with the query syntax described below, add a **Raw Filter**. Open the **+** menu and pick **Raw Filter**, or type your query in the menu's search box and choose **Create Raw Filter**. Press Enter to apply it, or Escape to cancel. The **?** button opens this page.

![Raw filter with a query using OR and an exclusion](/assets/img/docs/filtering-and-searching-raw-filter-light.webp)

Raw filters are combined with the date range and the other filters in the view.

You can also press `/` to open the command menu and search events and stacks from anywhere in the app.

## Save a View

When you change a view's filters, a dot appears on the **View** button to show you have unsaved changes. Open the **View** menu to:

- **Save** your changes to the current view.
- **Save As...** to create a new view. Give it a name and choose whether it is **Private** (only visible to you). The new view shows up in the sidebar.
- **Reset to Saved** to discard your changes.
- **Set as my home view** to open this view when you open Exceptionless.

![View menu with options to save, reset, and set a home view](/assets/img/docs/filtering-and-searching-save-view-light.webp)

## Searchable Fields & Requirements

| TERM               | EXAMPLE                                                                                                              | FIELD REQUIRED? (field:term) | DESCRIPTION                                   |
| ------------------ | -------------------------------------------------------------------------------------------------------------------- | ---------------------------- | --------------------------------------------- |
| `*`                | `*`                                                                                                                  | false                        | Matches everything. Status filters still apply |
| id                 | `id:54d8315ce6bb2d0500bcc7b4`                                                                                        | true                         | Documents id                                  |
| organization       | `organization:54d8315ce6bb2d0500bcc7b4`                                                                              | true                         | Organization id                               |
| project            | `project:54d8315ce6bb2d0500bcc7b4`                                                                                   | true                         | Project id                                    |
| stack              | `stack:54d8315ce6bb2d0500bcc7b4`                                                                                     | true                         | Stack id                                      |
| reference          | `reference:12345678`                                                                                                 | true                         | Reference id                                  |
| ref.session        | `ref.session:12345678`                                                                                               | true                         | Session id                                    |
| environment        | `environment:production` or `_missing_:environment` | true | Deployment environment |
| type               | `type:error`                                                                                                         | true                         | Event type                                    |
| source             | `source:"my log source"` or `"my log source"`                                                                        | false                        | Event source                                  |
| level              | `level:Error`                                                                                                        | true                         | Log level                                     |
| date               | `date:"2020-10-16T12:00:00.000"`                                                                                     | true                         | Occurrence date                               |
| first              | `first:true`                                                                                                         | true                         | True if first occurrence of event             |
| message            | `message:"My error message"` or `"My error message"`                                                                 | false                        | Event message                                 |
| tag                | `tag:"Blake Niemyjski"` or `tag:Blake` or `blake`                                                                    | false                        | Tags                                          |
| value              | `value:1`                                                                                                            | true                         | Value of event (used in charts)               |
| geo                | `geo:75044~75mi` or `geo:[geohash1..geohash2]`                                                                       | true                         | Geo Location                                  |
| status             | `status:open` or `status:discarded` or `status:fixed` or `status:regressed` or `status:snoozed` or `status:ignored`  | true                         | Stack status                                  |
| version            | `version:1` or `version:1.0` or `version:1.0.0`                                                                      | true                         | Application version                           |
| machine            | `machine:Server`                                                                                                     | true                         | Machine name                                  |
| ip                 | `ip:127.0.0.1`                                                                                                       | true                         | IP address                                    |
| architecture       | `architecture:x64`                                                                                                   | true                         | Machine architecture                          |
| useragent          | `useragent:IE` or `useragent:"Mozilla/5.0"`                                                                          | true                         | User Agent                                    |
| path               | `path:"/cart"` or `"/cart"`                                                                                          | false                        | URL path                                      |
| browser            | `browser:Chrome`                                                                                                     | true                         | Browser                                       |
| browser.version    | `browser.version:50.0`                                                                                               | true                         | Browser version                               |
| browser.major      | `browser.major:50`                                                                                                   | true                         | Browser major version                         |
| device             | `device:iPhone`                                                                                                      | true                         | Device                                        |
| os                 | `os:iOS`                                                                                                             | true                         | Operating System                              |
| os.version         | `os.version:8.0`                                                                                                     | true                         | Operating System version                      |
| os.major           | `os.major:8`                                                                                                         | true                         | Operating System major version                |
| count              | `count:>5`                                                                                                           | true                         | Number of occurrences the event represents    |
| client.version     | `client.version:4.0.0`                                                                                               | true                         | Exceptionless client version                  |
| cmd                | `cmd:"MyApp.exe --run"`                                                                                              | true                         | Command line                                  |
| country            | `country:US`                                                                                                         | true                         | Country                                       |
| level1             | `level1:TX`                                                                                                          | true                         | Region                                        |
| level2             | `level2:Dallas`                                                                                                      | true                         | City                                          |
| bot                | `bot:true`                                                                                                           | true                         | bot                                           |
| error.code         | `error.code:500` or `500`                                                                                            | false                        | Error code                                    |
| error.message      | `error.message:"A NullReferenceException occurred"`                                                                  | true                         | Error message                                 |
| error.type         | `error.type:"System.NullReferenceException"` or `"System.NullReferenceException"`                                    | false                        | Error type                                    |
| error.targettype   | `error.targettype:"System.NullReferenceException"` or `"System.NullReferenceException"`                              | false                        | Error target type                             |
| error.targetmethod | `error.targetmethod:AssociateWithCurrentThread` or `AssociateWithCurrentThread`                                      | false                        | Error target method                           |
| user               | `user:"random user identifier"` or `"random user identifier"`                                                        | false                        | Uniquely identifies user                      |
| user.name          | `user.name:"Exceptionless User"` or `"Exceptionless User"`                                                           | false                        | Friendly name of user                         |
| user.description   | `user.description:"I clicked the button"` or `"I clicked the button"`                                                | false                        | User Description                              |
| user.email         | `user.email:"support@exceptionless.io"` or `"support@exceptionless.io"`                                              | false                        | User email address (the user identity)        |

## Multiple Queries

All queries separated by a space will be an `AND` operation. If you wish to `OR` queries you’ll need to use an `OR` statement. We recommend wrapping conditional statements with parentheses.

**Example:** Let's assume we want to return all events that have a `blue` or `red` tag. To search for these events our query would be `(tag:blue OR tag:red)`.

## Wild Cards

Suffix your query with `*` for wild card searches.

## Exclusions

Prefix the field name with `-` for exclusions.

**Example:** Let's assume that we want to return all events that are not marked as a bot. To search for these events our query would be `-bot:true`.
*NOTE: In some cases searching with `-bot:true` is more accurate than searching with `bot:false`. This happens because the first query returns all records where `bot` field is `not set` or `not equal to true`. The second query returns results only where the `bot` field is set to `false`.

## Set and Unset Fields

Prefix the field name with `_missing_` or `_exists_`.

**Example:** Let's assume that we want to return all events that do not contain any tags. To search for these events our query would be `_missing_:tag`.

## Ranges

Specify a `date` or `numeric` range as part of the term.

**Date Range Example:** Let's assume that we want to return all events that occurred in 2020. To search for these events our query would be `date:[2020-01-01 TO 2020-12-31]`.

**Numeric Range Example:** Let's assume that we want to return all events that contain a `value` between 1 and 10. To search for these events our query would be `value:(>0 AND <=10)`.

## Custom Extended Data

All simple data types (`string`, `boolean`, `date`, `number`) that are stored in extended data will be indexed. _NOTE_: Field names are lowercased. Any field name that contains characters other than letters, digits, and hyphens, or is longer than 25 characters, is not indexed.

**Example:** Let's assume that our events extended data contains a property called `Age` with a value of `18`. To search for this value our query would be `data.age:18`.

---

[Next > Bulk Actions](/docs/bulk-actions)
