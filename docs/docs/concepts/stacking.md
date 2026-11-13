---
title: "Stacking"
description: "How Exceptionless groups events into stacks: the signature for each event type, the project's stacking settings, and manual stacking keys."
redirectFrom:
  - from: /docs/manual-stacking/
    anchor: manual-stacking
sources:
  - src/Exceptionless.Core/Pipeline/010_AssignToStackAction.cs
  - src/Exceptionless.Core/Utility/ErrorSignature.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/03_ManualStackingPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/10_NotFoundPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/15_SourceMapPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/20_ErrorPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/30_SimpleErrorPlugin.cs
  - src/Exceptionless.Core/Plugins/Formatting/Default/05_ManualStackingFormattingPlugin.cs
  - src/Exceptionless.Core/Extensions/MethodExtensions.cs
  - src/Exceptionless.Core/Models/Data/ManualStackingInfo.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/settings/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-options-dropdown-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/stacking-information-dialog.svelte
  - Exceptionless.Net:src/Exceptionless/Extensions/EventBuilderExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/EventExtensions.cs
  - Exceptionless.JavaScript:packages/core/src/EventBuilder.ts
---

# Stacking

Exceptionless groups events that have the same cause into a stack, so a thousand occurrences of one bug show up as one
item with a count. This page explains how events are grouped, what you can change, and how to group events yourself.

## How a stack is chosen

When an event arrives, Exceptionless builds a set of named values that describe its cause, called the signature. It
hashes those values and looks for a stack in the same project with the same hash. If there is one, the event joins it.
If not, a new stack is created, and the event is its first occurrence.

A few things follow from this:

- Stacks belong to a project. The same error in two projects makes two stacks.
- The environment, version, tags, and user are not part of the signature. The same error in staging and production lands
  in one stack; filter by [environment](/docs/environments/) to tell them apart.
- The signature is set when the event is processed. Changing a stacking setting affects new events only; existing events
  stay in the stacks they joined, and a change can start new stacks for errors you already have.
- The stack title comes from the event that created the stack.

## Signatures by event type

| Event type                    | Signature values                                                                                                                                                                       |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Error with a stack trace      | `ExceptionType` and `Method`: the exception type and the method where it was thrown in your code. Also `Number` or `ErrorCode` when the error carries one, such as a SQL error number. |
| Simple error                  | `ExceptionType`, and `StackTrace` (a hash of the stack trace text). Custom clients send these as `@simple_error`.                                                                      |
| Log (`log`)                   | `Type` and `Source`. The message is not included, so every message from one source, such as a logger category, goes to the same stack.                                                 |
| Feature usage (`usage`)       | `Type` and `Source`, where the source is the feature name.                                                                                                                             |
| Not found (`404`)             | `Type` and `Source`, where the source is the resource or request path, such as `GET /missing-page`.                                                                                    |
| Any other type                | `Type` and `Source`.                                                                                                                                                                   |
| Any event with a stacking key | Only the [manual stacking](#manual-stacking) values. Nothing else is used, not even the type.                                                                                          |

### How the method of an error is found

For errors, Exceptionless looks for the method in your code that threw. It starts at the innermost exception and walks
out through the inner exceptions, reading each stack trace from the top. The first frame that counts as your code is the
stacking target:

- If the project has **User Namespaces** set, a frame is your code when its namespace starts with one of them.
- Otherwise, any frame whose namespace doesn't start with `System` or `Microsoft` is your code. A frame with no
  namespace, as in most JavaScript stack traces, also counts.
- Frames whose method signature contains one of the project's **Common Methods** are skipped.

When no frame qualifies, Exceptionless uses the innermost exception's target method, then its top stack frame, and
finally the exception type alone.

The method value is the full method signature: namespace, type, method name, generic arguments, and parameter types. It
has no line numbers, so editing the body of a method keeps its errors in the same stack. Renaming the method or changing
its parameters starts a new one. The error message isn't part of the signature, so one exception type thrown from one
method with different messages is a single stack.

For JavaScript errors, [source maps](/docs/clients/javascript/source-maps/) are applied before the signature is built,
so stacks use your original function names or, for anonymous functions, the original file, line, and column. A new build
with different minified names doesn't start new stacks.

## Stacking settings

Each project has two settings that change how errors are stacked. Open the project's **Settings** page under **Project
Settings** and find **Error Stacking**:

- **User Namespaces**: a comma-delimited list of the namespaces your code belongs to, such as `Contoso`. When set, only
  methods in these namespaces can be stacking targets. Set it when library or framework frames, which would otherwise
  count as your code, are becoming the stacking target.
- **Common Methods**: a comma-delimited list of method names that should never be stacking targets, such as `Assert` or
  `ThrowIfInvalid`. Use it for shared helpers that throw on behalf of their callers, so errors stack by the code that
  called the helper instead of all landing in one stack. When the setting is empty, `DataContext.SubmitChanges` and
  `Entities.SaveChanges` are skipped; setting a value replaces those defaults.

See [Projects](/docs/project-settings/) for the rest of the project settings.

## See why events were grouped

To see the values that grouped a stack, open the stack, click the **Stack options** gear button next to the status
button, and choose **View Stacking Information**.

![The Stack Options menu with View Stacking Information](/assets/img/docs/managing-stacks-options-menu-light.webp)

The **Stacking Information** dialog lists each signature value, such as `ExceptionType` and `Method` for an error,
`Type` and `Source` for a log, or your own key for a manually stacked event.

## Manual stacking {#manual-stacking}

Set a manual stacking key when you want to choose the grouping yourself. Every event in a project with the same key goes
to the same stack, regardless of its type, message, or stack trace. Some uses:

- Keep all errors from a new or critical feature in one stack, so you can watch them and set
  [notifications](/docs/notifications/) on it.
- Split log messages that share a source into separate stacks.
- Group events that Exceptionless would put in different stacks, such as the same failure thrown from several places.

You can also give the stack a title. The title is used when the stack is created; otherwise the stack gets its usual
title.

```csharp tab=".NET"
using Exceptionless;

try {
    throw new ApplicationException("Unable to create order from quote.");
} catch (Exception ex) {
    ex.ToExceptionless()
        .SetManualStackingKey("Order creation", "order-creation")
        .Submit();
}
```

```ts tab="JavaScript"
import { Exceptionless } from "@exceptionless/browser" // or "@exceptionless/node"

try {
  throw new Error("Unable to create order from quote.")
} catch (error) {
  await Exceptionless.createException(error)
    .setManualStackingKey("order-creation", "Order creation")
    .submit()
}
```

```bash tab="curl"
curl -X POST https://api.exceptionless.io/api/v2/events \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "error",
    "message": "Unable to create order from quote.",
    "@simple_error": {
      "message": "Unable to create order from quote.",
      "type": "System.ApplicationException",
      "stack_trace": "   at Orders.QuoteService.CreateOrder()"
    },
    "@stack": {
      "title": "Order creation",
      "signature_data": { "ManualStackingKey": "order-creation" }
    }
  }'
```

The title is optional. Note the argument order: in .NET it's `SetManualStackingKey(title, key)` or
`SetManualStackingKey(key)`, and in JavaScript it's `setManualStackingKey(key, title?)`.

To group by more than one value, pass a dictionary of values instead: `SetManualStackingInfo` in .NET and
`setManualStackingInfo` in JavaScript. Over HTTP, put any keys you like in `signature_data` under the `@stack` data key.
You can also set the key from a client plugin for every event that matches a condition; see
[.NET plugins](/docs/clients/dotnet/plugins/).

Manual stacking replaces the automatic signature, so the stacking settings above don't apply to these events.

## Related pages

- [Stacks](/docs/managing-stacks/): fix, snooze, ignore, discard, and delete stacks.
- [Releases and regressions](/docs/versioning/): what happens when a fixed stack gets a new event.
- [Event format](/docs/reference/event-format/): the `@error`, `@simple_error`, and `@stack` data keys.
