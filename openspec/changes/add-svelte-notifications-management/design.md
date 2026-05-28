# Design: Add Svelte Notifications Management

## Backend

### NotificationService extraction

Extract shared notification logic from `StatusController` into a service to avoid duplication:

```csharp
// src/Exceptionless.Core/Services/NotificationService.cs
public class NotificationService(ICacheClient cacheClient, IMessagePublisher messagePublisher, TimeProvider timeProvider)
{
    public Task<SystemNotification?> GetSystemNotificationAsync();
    public Task<SystemNotification> SetSystemNotificationAsync(string message, bool publish = true);
    public Task ClearSystemNotificationAsync(bool publish = true);
    public Task<ReleaseNotification> SendReleaseNotificationAsync(string? message, bool critical);
}
```

- Cache key: `system-notification` (no TTL — persists until explicitly cleared)
- Publish: via `IMessagePublisher` (unchanged)
- `StatusController` delegates to this service (behavior unchanged for existing endpoints)

### New StatusController endpoints

Two new endpoints added to `StatusController` (all notification routes live under `/api/v2/notifications/`):

| Method | Route | Auth | Body | Response |
|--------|-------|------|------|----------|
| GET | `notifications/settings` | GlobalAdminPolicy | — | `NotificationSettingsResponse` |
| POST | `notifications/force-refresh` | GlobalAdminPolicy | optional `{ value: string }` | `ReleaseNotification` |

Existing endpoints updated with `bool publish = true` query parameter:

| Method | Route | Change |
|--------|-------|--------|
| POST | `notifications/system` | Added `?publish=true/false` |
| DELETE | `notifications/system` | Added `?publish=true/false` |

### DTO

`NotificationSettingsResponse` (in `src/Exceptionless.Core/Models/Messaging/`):

```csharp
public record NotificationSettingsResponse
{
    public string? ConfiguredSystemNotificationMessage { get; init; }
    public SystemNotification? SystemNotification { get; init; }
}
```

Serialized as snake_case via the project's `LowerCaseUnderscoreNamingPolicy`:
- `configured_system_notification_message`
- `system_notification`

### Message length validation

All notification message inputs are capped at **1000 characters** via `MaxNotificationMessageLength` constant in `StatusController`. Applies to `PostSystemNotificationAsync`, `PostReleaseNotificationAsync`, and `ForceRefreshAsync`.

### Authorization

- `GET notifications/system` — UserPolicy (banner shown to all logged-in users)
- All other notification endpoints — GlobalAdminPolicy

## Frontend (Svelte)

### Feature module

`src/Exceptionless.Web/ClientApp/src/lib/features/system-notifications/`

- `models.ts` — TypeScript DTOs matching snake_case API serialization
- `api.svelte.ts` — TanStack Query wrappers using `useFetchClient`
- `resolve-message.ts` — Pure function for three-tier message resolution
- `force-refresh-coordinator.ts` — Module-level flag to handle admin self-reload race
- `components/system-notification-banner.svelte` — Banner rendering component

### Three-tier message resolution

Implemented in `resolve-message.ts`:

```
Priority (highest to lowest):
1. realtimeMessage — from WebSocket (undefined = not yet received, null = explicitly cleared)
2. persistedMessage — from GET /notifications/system on mount
3. fallbackMessage — from PUBLIC_SYSTEM_NOTIFICATION_MESSAGE env var
```

When `realtimeMessage` is `null` (explicit WebSocket clear), the persisted value is **skipped** — only the env fallback can show. This is intentional: the WebSocket clear signals "clear the dynamic notification" not "suppress all messages including env".

### Force-refresh self-reload coordinator

A module-level flag (`force-refresh-coordinator.ts`) prevents the initiating admin tab from reloading before the success toast renders:

- `flagSelfInitiatedForceRefresh()` — called by the admin page before the API request
- `consumeSelfInitiatedFlag()` — called by the banner on critical `ReleaseNotification`
- If flag was set: delay reload by **1500ms** (toast-visible window) then reload
- If flag was not set (other clients): reload **immediately**

### Notification banners

Rendered in `src/Exceptionless.Web/ClientApp/src/routes/(app)/+layout.svelte`.

Behavior:
1. On mount: fetch `GET /api/v2/notifications/system` (30s staleTime; WebSocket is primary realtime path)
2. Fallback: `PUBLIC_SYSTEM_NOTIFICATION_MESSAGE` env var
3. Listen for `SystemNotification` and `ReleaseNotification` DOM CustomEvents
4. System notification → destructive Alert with `role="alert"` and `aria-live="assertive"`
5. Release notification → info Alert with `role="status"` and `aria-live="polite"`
6. Critical release → page reload (immediate for non-initiators, 1500ms delayed for initiating admin)

Security: Plain text only. No `{@html}`.

### Admin page

Route: `/system/notifications`
- Added to system nav in `routes.svelte.ts`, visible to global admins only
- Card layout showing current state (configured fallback + active notification)
- Dialog-based actions: set notification, clear, send release, force refresh
- All textareas enforce `maxlength={1000}` matching backend validation
- Uses shadcn-svelte Alert, Button, Dialog, Textarea, Card
- Mutations use svelte-sonner for success/error toasts
- Invalidates queries on mutation success

### Query keys

```typescript
export const queryKeys = {
    current: ['notifications', 'system'] as const,
    settings: ['notifications', 'settings'] as const
};
```

## Security

- Admin endpoints behind GlobalAdminPolicy
- No HTML injection: plain text rendering only
- Message length capped at 1000 chars (both frontend maxlength and backend validation)
- No secrets exposed in admin response (only configured message text)

## Accessibility

- Banners use `role="alert"` / `role="status"` with appropriate `aria-live`
- Color is not sole indicator (icon + text in banners)
