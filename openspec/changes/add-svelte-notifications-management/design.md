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

- Cache key: `system-notification` (unchanged)
- Publish: via `IMessagePublisher` (unchanged)
- `StatusController` delegates to this service (behavior unchanged)

### Admin endpoints

Added to `AdminController` (already `[Authorize(Policy = GlobalAdminPolicy)]`):

| Method | Route | Body | Response |
|--------|-------|------|----------|
| GET | `admin/notifications` | — | `{ configured_message, system_notification }` |
| PUT | `admin/notifications/system` | `{ message }` | `SystemNotification` |
| DELETE | `admin/notifications/system?publish=true` | — | 204 |
| POST | `admin/notifications/release` | `{ message, critical }` | `ReleaseNotification` |
| POST | `admin/notifications/force-refresh` | — | `ReleaseNotification` |

DTOs in `src/Exceptionless.Web/Models/Admin/`:
- `NotificationSettingsResponse` — configured fallback + current persisted notification
- `SetSystemNotificationRequest` — message string
- `SendReleaseNotificationRequest` — message + critical flag

### Authorization

All admin endpoints require `GlobalAdminPolicy` (existing controller-level attribute).

## Frontend (Svelte)

### Feature module

`src/Exceptionless.Web/ClientApp/src/lib/features/notifications/`

- `models.ts` — Re-export websocket models + admin DTOs
- `api.svelte.ts` — TanStack Query wrappers using `useFetchClient`
- `components/notification-banners.svelte` — Banner rendering component

### Notification banners

Rendered in `src/Exceptionless.Web/ClientApp/src/routes/(app)/+layout.svelte`.

Behavior:
1. On mount: fetch `GET /api/v2/notifications/system` (existing StatusController endpoint)
2. Fallback: if no persisted notification, use `PUBLIC_SYSTEM_NOTIFICATION_MESSAGE` env var
3. Listen for `SystemNotification` and `ReleaseNotification` DOM CustomEvents (existing websocket dispatch)
4. System notification → destructive/danger Alert with `role="alert"` and `aria-live="assertive"`
5. Release notification → info Alert with `role="status"` and `aria-live="polite"`
6. Critical release → `window.location.reload()` immediately

Security: Plain text only. No `{@html}`. If sanitization is needed later, use dompurify.

### Admin page

Route: `/system/notifications`
- Added to system nav in `routes.svelte.ts`, visible to global admins only
- Card layout showing current state
- Dialog-based actions: set notification, clear, send release, force refresh
- Uses shadcn-svelte Alert, Button, Dialog, Input/Textarea, Card
- Mutations use svelte-sonner for success/error toasts
- Invalidates queries on mutation success

### Query keys

```typescript
export const notificationKeys = {
  settings: ['admin', 'notifications'] as const,
  current: ['notifications', 'system'] as const,
};
```

## Security

- Admin endpoints already behind GlobalAdminPolicy
- No HTML injection: plain text rendering only
- No secrets exposed in admin response (only configured message text)

## Accessibility

- Banners use `role="alert"` / `role="status"` with appropriate `aria-live`
- Dismiss button (if added) is keyboard-accessible
- Color is not sole indicator (icon + text in banners)
