---
title: "Upgrading"
order: 11
---

# Upgrading

- [Upgrading from Exceptionless 5.x](#upgrading-from-exceptionless-5x)
- [Upgrading from Exceptionless 4.x](#upgrading-from-exceptionless-4x)
  - [ProcessQueueAsync IAsyncDisposable pattern](#processqueueasync-iasyncdisposable-pattern)
- [Upgrading from Exceptionless 3.x](#upgrading-from-exceptionless-3x)
- [Upgrading from Exceptionless 2.x](#upgrading-from-exceptionless-2x)

## Upgrading from Exceptionless 5.x

We bumped the major version due to serialization changes we made under the hood.
We now only apply snake case naming strategy to known exceptionless models. Any
extra data like custom exception properties or user defined data is preserved
exactly as is. We also made changes to ensure that empty collections and
dictionaries are now serialized as they were previously excluded.

## Upgrading from Exceptionless 4.x

Here is a breakdown of the breaking changes in each package

- Exceptionless Package
  - `ExceptionlessClient`
    - renamed `UpdateUserEmailAndDescription` to `UpdateUserEmailAndDescriptionAsync` and made it async.
    - removed `ProcessQueue`, replace this call with the async version `ProcessQueueAsync`.
    - removed `ProcessQueueDeferred`, we recommend calling `ProcessQueueAsync` in `IAsyncDisposable` pattern. The section [below](#processqueueasync-iasyncdisposable-pattern) contains more information.
    - renamed `Shutdown` extension method to `ShutdownAsync` and made it async.
    - renamed `SubmitSessionEnd` extension method to `SubmitSessionEndAsync` and made it async.
    - renamed `SubmitSessionHeartbeat` extension method to `SubmitSessionHeartbeatAsync` and made it async.
  - `SettingsManager`
    - renamed `CheckVersion` to `CheckVersionAsync` and made it async.
    - renamed `UpdateSettings` to `UpdateSettingsAsync` and made it async.
  - `DefaultEventQueue`
    - removed `Process`, replace this call with the async version `ProcessAsync`.
  - `ProcessQueueScope`
    - removed this class, we recommend calling `await client.ProcessQueueAsync` in `IAsyncDisposable` pattern. The section [below](#processqueueasync-iasyncdisposable-pattern) contains more information.
  - `ISubmissionClient`
    - removed `PostEvents`, replace this call with the async version `PostEventsAsync`.
    - removed `PostUserDescription`, replace this call with the async version `PostUserDescriptionAsync`.
    - removed `GetSettings`, replace this call with the async version `GetSettingsAsync`.
    - removed `SendHeartbeat`, replace this call with the async version `SendHeartbeatAsync`.
- Exceptionless.WebApi Package
  - `ExceptionlessClient extension methods`
    - renamed `UnregisterWebApi` to `UnregisterWebApiAsync` and made it async.
- Exceptionless.Windows Package
  - `ExceptionlessClient extension methods`
    - renamed `Unregister` to `UnregisterAsync` and made it async.
- Exceptionless.Wpf Package
  - `ExceptionlessClient extension methods`
    - renamed `Unregister` to `UnregisterAsync` and made it async.

### ProcessQueueAsync IAsyncDisposable pattern

We removed `ProcessQueueDeferred` as it was doing asynchronous work inside of a synchronous dispose. The following pattern solves this issue.

> We may introduce a this pattern in the core library at a later date targeting
> .NET Core, we just didn't want to take on extra package dependencies, of
> which could become a diamond dependency.

The change is pretty simple, just upgrade existing code calling `ProcessQueueDeferred`

```csharp
using var _ = client.ProcessQueueDeferred();
```

and replace it with the following:

```csharp
await using var _ = new ProcessQueueScope(client);

internal class ProcessQueueScope : IAsyncDisposable {
    private readonly ExceptionlessClient _exceptionlessClient;

    public ProcessQueueScope(ExceptionlessClient exceptionlessClient) {
        _exceptionlessClient = exceptionlessClient;
    }

    public async ValueTask DisposeAsync() {
        await _exceptionlessClient.ProcessQueueAsync();
    }
}

```

We recommend creating a `ProcessQueueScope` as a reusable utility class and feel free to rename it!

## Upgrading from Exceptionless 3.x

- The `Exceptionless.Portable` package and `Exceptionless.Extras` assembly was merged into the `Exceptionless` package.

## Upgrading from Exceptionless 2.x

- `IEventEnrichment` has been renamed to `IEventPlugin`
- `IEventPlugin.Enrich(context, event)` signature has been changed to `IEventPlugin.Run(context)`. The event has been moved to the context
- `client.Configuration.AddEnrichment&lt;IEventEnrichment&gt;();` has been renamed to `client.Configuration.AddPlugin&lt;IEventPlugin&gt;();`
- `EventPluginContext.Data` property has been renamed to `EventPluginContext.ContextData`
- `EventSubmittingEventArgs.EnrichmentContextData` property has been renamed to `EventSubmittingEventArgs.PluginContextData`

---

[Next > JavaScript Client](/docs/clients/javascript/)
