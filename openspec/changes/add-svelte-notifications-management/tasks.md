# Tasks: Add Svelte Notifications Management

## Backend

- [x] **Task 1: Extract NotificationService**
  - Created `src/Exceptionless.Core/Services/NotificationService.cs`
  - Methods: `GetSystemNotificationAsync`, `SetSystemNotificationAsync`, `ClearSystemNotificationAsync`, `SendReleaseNotificationAsync`
  - Registered as singleton in DI (`Bootstrapper.cs`)
  - Refactored `StatusController` to delegate to `NotificationService` (no behavior change)

- [x] **Task 2: Add new StatusController endpoints**
  - `GET notifications/settings` — returns configured fallback + current notification (admin only)
  - `POST notifications/force-refresh` — force reload all clients via critical release notification (admin only)
  - Added `bool publish = true` query param to POST and DELETE system notification endpoints
  - All endpoints use existing `ValueFromBody<string>` pattern for backward compatibility

- [x] **Task 3: Update HTTP test samples**
  - Updated `tests/http/status.http` with force-refresh and settings endpoint samples

- [x] **Task 4: Add backend integration tests**
  - Added tests to existing `tests/Exceptionless.Tests/Controllers/StatusControllerTests.cs`
  - Tests: settings (admin/non-admin), force-refresh (with message/without/non-admin), publish flag, message length limits
  - All tests follow AAA (Arrange/Act/Assert) pattern

## Frontend — System Notification Banner

- [x] **Task 5: Create system-notifications feature module**
  - Created `src/Exceptionless.Web/ClientApp/src/lib/features/system-notifications/models.ts`
  - Created `src/Exceptionless.Web/ClientApp/src/lib/features/system-notifications/api.svelte.ts`
  - All API calls target StatusController routes (`notifications/*`)

- [x] **Task 6: Create system-notification-banner component**
  - Created `src/Exceptionless.Web/ClientApp/src/lib/features/system-notifications/components/system-notification-banner.svelte`
  - Three-tier message resolution: realtime WebSocket → persisted API → env fallback (implemented in `resolve-message.ts`)
  - System = destructive Alert with `role="alert"` `aria-live="assertive"`
  - Release = info Alert with `role="status"` `aria-live="polite"`
  - Critical release = `window.location.reload()` (immediate for all clients; 1500ms delayed on initiating admin tab via `force-refresh-coordinator.ts`)

- [x] **Task 6b: Add force-refresh coordinator**
  - Created `src/Exceptionless.Web/ClientApp/src/lib/features/system-notifications/force-refresh-coordinator.ts`
  - Prevents admin self-reload race: flags are set before API call, consumed in banner's critical handler

- [x] **Task 7: Integrate banner into app layout**
  - Added `<SystemNotificationBanner />` to `(app)/+layout.svelte`

- [x] **Task 8: Add unit tests**
  - `resolve-message.test.ts` — 9 tests for `resolveDisplayMessage` three-tier resolution logic
  - `force-refresh-coordinator.test.ts` — 4 tests for the self-initiated reload flag

## Frontend — Admin Page

- [x] **Task 9: Add System → Notifications route and nav entry**
  - Created `src/Exceptionless.Web/ClientApp/src/routes/(app)/system/notifications/+page.svelte`
  - Added Bell icon + Notifications nav entry in `routes.svelte.ts`, visible only to global admins

- [x] **Task 10: Implement notifications admin page**
  - Displays current state (configured fallback, persisted notification)
  - Actions via dialogs: Set system notification, Clear/reset, Send release notification, Force refresh
  - Uses shadcn-svelte components, svelte-sonner toasts, TanStack Query mutations
  - Invalidates notification queries on success

## Final Validation

- [x] **Task 11: Full build and test validation**
  - `dotnet build` ✓
  - `dotnet test -- --filter-class StatusControllerTests` → 19/19 pass ✓
  - `npm run check` (svelte-check) → 0 errors ✓
  - `npm run lint` → clean ✓
  - `npm run build` → success ✓
  - `npm run test:unit` → 249/249 pass ✓
  - UI dogfood via browser → all flows verified ✓
