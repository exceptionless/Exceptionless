---
title: "Search syntax"
description: "Every field you can search in Exceptionless, with operators, ranges, wildcards, custom data, and examples for raw filters and the API."
sources:
  - src/Exceptionless.Core/Repositories/Configuration/Indexes/EventIndex.cs
  - src/Exceptionless.Core/Repositories/Configuration/Indexes/StackIndex.cs
  - src/Exceptionless.Core/Repositories/Queries/Visitors/EventFieldsQueryVisitor.cs
  - src/Exceptionless.Core/Repositories/Queries/Visitors/EventStackFilterQueryVisitor.cs
  - src/Exceptionless.Core/Repositories/Queries/Validation/PersistentEventQueryValidator.cs
  - src/Exceptionless.Core/Repositories/Queries/Validation/StackQueryValidator.cs
  - src/Exceptionless.Core/Repositories/Queries/Validation/EventStackQueryValidator.cs
  - src/Exceptionless.Core/Extensions/PersistentEventExtensions.cs
  - src/Exceptionless.Core/Extensions/StringExtensions.cs
  - src/Exceptionless.Core/Pipeline/035_CopySimpleDataToIdxAction.cs
  - src/Exceptionless.Web/Api/Infrastructure/ApiFilterPolicy.cs
  - src/Exceptionless.Web/Api/Infrastructure/TimeRangeParser.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/premium-filter.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/date-range-picker/date-range-picker.svelte
  - tests/Exceptionless.Tests/Search/EventIndexTests.cs
  - tests/Exceptionless.Tests/Search/EventStackFilterTests.cs
---

# Search syntax

Exceptionless filters use a Lucene-style query syntax. You type it into a **Raw Filter** in the app (see
[Filtering and searching](/docs/filtering-and-searching/)), pass it as the `filter` parameter to the
[REST API](/docs/api/getting-events/), and Exie and the [MCP server](/docs/ai/mcp-server/) use it in their tools.

```text
type:error environment:production (tag:Billing OR tag:Checkout) -bot:true
```

The time range is separate from the filter. The app sets it with the date range picker and the API with the `time`
parameter. See [Date ranges](#date-ranges).

## Operators

| Syntax                      | Meaning                                                                         | Example                                        |
| --------------------------- | ------------------------------------------------------------------------------- | ---------------------------------------------- |
| `field:value`               | The field matches the value.                                                    | `type:error`                                   |
| `field:"two words"`         | Quote values that contain spaces or special characters.                         | `message:"Invalid hash. Parameter name: hash"` |
| `a b` or `a AND b`          | Both terms must match. A space means AND.                                       | `type:log level:error`                         |
| `a OR b`                    | Either term matches. Wrap OR groups in parentheses.                             | `(tag:blue OR tag:red)`                        |
| `field:(a OR b)`            | Either value in one field.                                                      | `level:(error OR fatal)`                       |
| `-field:value`              | Excludes matches. `NOT` and `!` work too.                                       | `-bot:true`                                    |
| `_exists_:field`            | The field has a value.                                                          | `_exists_:environment`                         |
| `_missing_:field`           | The field has no value.                                                         | `_missing_:tag`                                |
| `value*`                    | Starts with. Only a trailing wildcard is supported.                             | `reference:order-*`                            |
| `field:>n`, `>=`, `<`, `<=` | Comparisons on numbers and dates.                                               | `count:>5`                                     |
| `field:[a TO b]`            | An inclusive range. Use `{a TO b}` to exclude the ends and `*` for an open end. | `value:[1 TO 10]`                              |

`-bot:true` and `bot:false` aren't the same: `-bot:true` also matches events that don't have the field at all.

Write field names in lowercase, as shown here; some of them are case-sensitive. Whether a value is case-sensitive
depends on the field, as noted in the tables below.

## Event fields

These fields work in the **Stacks**, **Events**, and **Sessions** lists in the app and with the `/api/v2/events`
endpoints. "Words" means the field matches any word in the value; quote the value to match a phrase. "Exact" means the
whole value must match, including case. "Whole value" means the whole value must match, ignoring case.

| Field                | What it matches                                                                                    | Match       | Example                                         |
| -------------------- | -------------------------------------------------------------------------------------------------- | ----------- | ----------------------------------------------- |
| `id`                 | Event ID                                                                                           | Exact       | `id:54dbc16ca0f5c61398427b00`                   |
| `reference`          | The event's [reference ID](/docs/references-ids/)                                                  | Exact       | `reference:876554321`                           |
| `stack`              | Stack ID                                                                                           | Exact       | `stack:1ecd0826e447a44e78877ab1`                |
| `project`            | Project ID                                                                                         | Exact       | `project:537650f3b77efe23a47914f4`              |
| `organization`       | Organization ID                                                                                    | Exact       | `organization:537650f3b77efe23a47914f3`         |
| `type`               | Event type: `error`, `log`, `usage`, `404`, `session`, `heartbeat`, `sessionend`, or a custom type | Whole value | `type:usage`                                    |
| `environment`        | The deployment [environment](/docs/environments/)                                                  | Whole value | `environment:production`                        |
| `source`             | Log source, feature name, or other event source                                                    | Words       | `source:"GET /Print"`                           |
| `message`            | Event message                                                                                      | Words       | `message:timeout`                               |
| `tag`                | A tag                                                                                              | Exact       | `tag:"Blake Niemyjski"`                         |
| `date`               | When the event occurred                                                                            | Date        | `date:>now-1h`                                  |
| `first`              | `true` for the first event in its stack                                                            | Boolean     | `first:true`                                    |
| `value`              | The event's numeric value                                                                          | Number      | `value:[1 TO 10]`                               |
| `count`              | How many occurrences the event stands for, after client-side deduplication                         | Number      | `count:>5`                                      |
| `level`              | Log level: `trace`, `debug`, `info`, `warn`, `error`, or `fatal`                                   | Whole value | `level:error`                                   |
| `version`            | Your app's version. See [Versions](#versions).                                                     | Version     | `version:2.4`                                   |
| `submission`         | How the client captured the event, such as `UnobservedTaskException`                               | Exact       | `submission:UnobservedTaskException`            |
| `client.version`     | Exceptionless client version                                                                       | Exact       | `client.version:6.2.0`                          |
| `client.useragent`   | Exceptionless client user agent                                                                    | Whole value | `client.useragent:"exceptionless/6.2.0"`        |
| `ip`                 | Client, request, or machine IP address                                                             | Words       | `ip:192.168.0.243`                              |
| `useragent`          | Request user agent                                                                                 | Words       | `useragent:"Mozilla/5.0"`                       |
| `path`               | Request path                                                                                       | Words       | `path:"/cart"`                                  |
| `browser`            | Browser name                                                                                       | Words       | `browser:"Chrome Mobile"`                       |
| `browser.version`    | Browser version                                                                                    | Exact       | `browser.version:39.0.2171`                     |
| `browser.major`      | Browser major version                                                                              | Exact       | `browser.major:39`                              |
| `device`             | Device name                                                                                        | Words       | `device:"Huawei U8686"`                         |
| `bot`                | `true` when the request came from a bot                                                            | Boolean     | `bot:true`                                      |
| `os`                 | Operating system name                                                                              | Words       | `os:"Mac OS X"`                                 |
| `os.version`         | Operating system version                                                                           | Exact       | `os.version:10.10.1`                            |
| `os.major`           | Operating system major version                                                                     | Exact       | `os.major:10`                                   |
| `machine`            | Machine name                                                                                       | Whole value | `machine:SERVER-01`                             |
| `architecture`       | Machine architecture                                                                               | Exact       | `architecture:x64`                              |
| `cmd`                | Process command line                                                                               | Exact       | `cmd:"MyApp.exe --run"`                         |
| `country`            | Country                                                                                            | Whole value | `country:US`                                    |
| `level1`             | Region, such as a state                                                                            | Exact       | `level1:TX`                                     |
| `level2`             | City                                                                                               | Exact       | `level2:Dallas`                                 |
| `locality`           | Locality                                                                                           | Exact       | `locality:Downtown`                             |
| `user`               | The user's identity                                                                                | Words       | `user:jane@example.com`                         |
| `user.email`         | The user's email address (matches the user identity)                                               | Words       | `user.email:jane@example.com`                   |
| `user.name`          | The user's name                                                                                    | Whole value | `user.name:"Jane Doe"`                          |
| `user.description`   | The description a user submitted with an error                                                     | Words       | `user.description:"I clicked save"`             |
| `error.code`         | Error code                                                                                         | Exact       | `error.code:502`                                |
| `error.message`      | Error message                                                                                      | Words       | `error.message:"A task was canceled"`           |
| `error.type`         | Exception type, with or without its namespace                                                      | Type name   | `error.type:NullReferenceException`             |
| `error.targettype`   | Type of the method that threw                                                                      | Type name   | `error.targettype:System.Exception`             |
| `error.targetmethod` | Method that threw                                                                                  | Type name   | `error.targetmethod:AssociateWithCurrentThread` |
| `data.<name>`        | A custom data value. See [Custom data](#custom-data).                                              | Depends     | `data.orderid:12345`                            |
| `ref.<name>`         | A named event reference, such as a session                                                         | Exact       | `ref.session:abc123`                            |

The `error.*` fields match any exception in the chain, including inner exceptions. "Type name" fields match the full
name or any trailing part of it: `error.type:NullReferenceException` and `error.type:System.NullReferenceException` both
match a `System.NullReferenceException`.

### Search without a field

A term without a field name searches several fields at once: `id`, `reference`, `source`, `message`, `tag`, `path`,
`error.code`, `error.type`, `error.targettype`, `error.targetmethod`, `user`, `user.name`, and the email address and
description a user submitted with an error.

```text
NullReferenceException
"Invalid hash. Parameter name: hash"
```

## Stack fields

Event lists can also filter by fields of the stack an event belongs to. Exceptionless finds the matching stacks and
returns their events.

| Field              | What it matches                                                                  | Example                                         |
| ------------------ | -------------------------------------------------------------------------------- | ----------------------------------------------- |
| `status`           | Stack status: `open`, `fixed`, `regressed`, `snoozed`, `ignored`, or `discarded` | `(status:open OR status:regressed)`             |
| `fixed`            | `true` for fixed stacks. The same as `status:fixed`.                             | `fixed:true`                                    |
| `regressed`        | `true` for regressed stacks. The same as `status:regressed`.                     | `regressed:true`                                |
| `hidden`           | `true` for stacks that aren't open or regressed                                  | `hidden:false`                                  |
| `critical`         | `true` when future occurrences are marked critical                               | `critical:true`                                 |
| `title`            | Stack title                                                                      | `title:"GET /Print"`                            |
| `description`      | Stack description                                                                | `description:"checkout"`                        |
| `links`            | A stack's reference link                                                         | `links:"https://github.com/acme/api/issues/42"` |
| `version_fixed`    | The version the stack was marked fixed in                                        | `version_fixed:1.2.3`                           |
| `fixedon`          | When the stack was marked fixed                                                  | `fixedon:>now-7d`                               |
| `first_occurrence` | When the stack first occurred                                                    | `first_occurrence:>now-1d`                      |
| `last`             | When the stack last occurred                                                     | `last:<now-30d`                                 |
| `occurrences`      | Total events in the stack, all time                                              | `occurrences:>100`                              |
| `signature`        | Stack signature hash                                                             | `signature:abc123`                              |

`type`, `tag`, `project`, `stack`, and `organization` apply to both the event and its stack.

Most of the app's built-in views include `(status:open OR status:regressed)` as a hidden filter, so fixed, snoozed,
ignored, and discarded stacks stay out of them. A raw filter in a view is combined with the view's hidden **Status**
filter. A filter sent to the API has no default status, so add one yourself.

## Versions {#versions}

`version` compares the parts of a version, so `version:1` matches `1.0`, `1.2.3`, and `1.9`, and `version:1.2` matches
`1.2.0` and `1.2.3`, but not `1.20`. Four-part versions such as `1.2.3.0` match `version:1.2.3`. Prerelease versions
match exactly, as in `version:3.2.1-beta1`.

Version ranges such as `version:>1.2` aren't supported. `browser.version`, `os.version`, `client.version`, and
`version_fixed` are plain values and only match exactly.

## Custom data {#custom-data}

Exceptionless indexes simple values in an event's custom data, so you can search them with `data.<name>`:

```text
data.orderid:12345
data.total:>100
data.isbeta:true
data.signupdate:>2025-01-01
data.customer:"Acme Corp"
```

- Only organizations on a paid plan get custom data indexed, and only for events received while they're on one.
- Only top-level keys are indexed. A key is indexed when it's 25 characters or fewer and has only letters, digits, and
  hyphens. Keys with underscores, dots, or spaces aren't indexed.
- Field names are lowercase in searches: a value sent as `OrderId` is searched as `data.orderid`.
- Strings, numbers, booleans, and dates are indexed. Objects, arrays, JSON strings, and strings of 1,000 characters or
  more aren't. A string that looks like `true`, `false`, a date, or a number is indexed as that type.
- String values are exact and case-sensitive.

Event references set with the client's `SetEventReference(name, id)` or `setEventReference(name, id)` are searchable as
`ref.<name>`, with the same key rules.

## Date ranges {#date-ranges}

Search a date field with a range or a comparison:

```text
date:[2025-01-01 TO 2025-01-31]
date:>=now-24h
fixedon:[now-7d TO now]
```

Date values can be dates and times, such as `2025-01-15` or `2025-01-15T09:30:00Z`, or date math from `now`: `now-1h`,
`now-7d`, `now-1M`, and `now/d` for the start of today. Units are `s`, `m`, `h`, `d`, `w`, `M`, and `y`.

The time range of a view, set with the app's date range picker or the API's `time` parameter, takes the same kind of
range, such as `[now-7d TO now]`, or `[now-1d/d TO now-1d/d]` for yesterday. There, a year or a month on its own, such
as `2025` or `2025-01`, covers the whole year or month.

## Free plan searches {#free-plan-searches}

On the Free plan, filters can use only these fields: `type`, `environment`, `status`, `date`, `reference`, `project`,
`stack`, and `organization`. Stack lists also allow `critical`, `first_occurrence`, `last`, and `last_occurrence`. Any
other field, a term without a field, or a `data.*` field needs a paid plan, and the app offers to upgrade when you use
one.

## Exie and the MCP server

Exie and the MCP tools accept the same syntax, but only for a fixed set of fields. A coding agent can call the MCP
server's `get_filter_fields` tool to list them. See [MCP server](/docs/ai/mcp-server/).
