# Tasks: Add Svelte Notifications Management

## Backend

- [ ] **Task 1: Extract NotificationService**
  - Create `src/Exceptionless.Core/Services/NotificationService.cs`
  - Methods: `GetSystemNotificationAsync`, `SetSystemNotificationAsync`, `ClearSystemNotificationAsync`, `SendReleaseNotificationAsync`
  - Register in DI
  - Refactor `StatusController` to delegate to `NotificationService` (no behavior change)
  - **Verify:** `dotnet build` passes; existing notification HTTP tests still pass

- [ ] **Task 2: Add admin notification DTOs**
  - Create `src/Exceptionless.Web/Models/Admin/NotificationSettingsResponse.cs`
  - Create `src/Exceptionless.Web/Models/Admin/SetSystemNotificationRequest.cs`
  - Create `src/Exceptionless.Web/Models/Admin/SendReleaseNotificationRequest.cs`
  - **Verify:** `dotnet build` passes

- [ ] **Task 3: Add admin notification endpoints to AdminController**
  - `GET admin/notifications` — returns settings + current notification
  - `PUT admin/notifications/system` — set system notification with validation
  - `DELETE admin/notifications/system?publish=true` — clear system notification
  - `POST admin/notifications/release` — send release notification
  - `POST admin/notifications/force-refresh` — critical release notification
  - All use `NotificationService`
  - **Verify:** `dotnet build` passes

- [ ] **Task 4: Add HTTP test samples**
  - Update or create `tests/http/admin.http` with requests for all new admin notification endpoints
  - **Verify:** File exists and contains all 5 endpoint samples

- [ ] **Task 5: Add backend integration tests**
  - Create `tests/Exceptionless.Tests/Controllers/AdminNotificationTests.cs`
  - Tests: admin can read/set/clear system notifications; non-admin gets 403; empty message rejected; release notification (critical and non-critical); force refresh; legacy StatusController endpoints still work
  - **Verify:** `dotnet test -- --filter-class Exceptionless.Tests.Controllers.AdminNotificationTests`

## Frontend — Notification Banners

- [ ] **Task 6: Create notification feature module**
  - Create `src/Exceptionless.Web/ClientApp/src/lib/features/notifications/models.ts` — re-export websocket models, admin DTOs
  - Create `src/Exceptionless.Web/ClientApp/src/lib/features/notifications/api.svelte.ts` — TanStack Query wrappers (`getCurrentSystemNotificationQuery`, `getNotificationSettingsQuery`, mutations)
  - **Verify:** `cd src/Exceptionless.Web/ClientApp && npm ci && npm run check`

- [ ] **Task 7: Create notification-banners component**
  - Create `src/Exceptionless.Web/ClientApp/src/lib/features/notifications/components/notification-banners.svelte`
  - Fetch persisted notification on mount; fallback to `PUBLIC_SYSTEM_NOTIFICATION_MESSAGE`
  - Listen for `SystemNotification` and `ReleaseNotification` DOM CustomEvents
  - System = destructive Alert with `role="alert"` `aria-live="assertive"`
  - Release = info Alert with `role="status"` `aria-live="polite"`
  - Critical release = `window.location.reload()`
  - Plain text only (no `{@html}`)
  - **Verify:** `cd src/Exceptionless.Web/ClientApp && npm run check`

- [ ] **Task 8: Integrate banners into app layout**
  - Add `<NotificationBanners />` to `src/Exceptionless.Web/ClientApp/src/routes/(app)/+layout.svelte`
  - **Verify:** `cd src/Exceptionless.Web/ClientApp && npm run check`

- [ ] **Task 9: Add notification banner unit tests**
  - Test: renders fallback when no persisted message
  - Test: renders persisted message from API
  - Test: DOM events update banners
  - Test: critical release calls reload
  - **Verify:** `cd src/Exceptionless.Web/ClientApp && npm run test:unit`

## Frontend — Admin Page

- [ ] **Task 10: Add System → Notifications route and nav entry**
  - Create `src/Exceptionless.Web/ClientApp/src/routes/(app)/system/notifications/+page.svelte`
  - Add nav entry in routes config, visible only to global admins
  - **Verify:** `cd src/Exceptionless.Web/ClientApp && npm run check`

- [ ] **Task 11: Implement notifications admin page**
  - Display current state (configured fallback, persisted notification)
  - Actions via dialogs: Set system notification, Clear/reset, Send release notification, Force refresh
  - Use shadcn-svelte components, svelte-sonner toasts, TanStack Query mutations
  - Invalidate notification queries on success
  - **Verify:** `cd src/Exceptionless.Web/ClientApp && npm run check && npm run build`

## Final Validation

- [ ] **Task 12: Full build and test validation**
  - `dotnet build`
  - `dotnet test`
  - `cd src/Exceptionless.Web/ClientApp && npm ci && npm run check && npm run lint && npm run build && npm run test:unit`
  - **Verify:** All commands pass with no errors
