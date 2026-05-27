# Spec: Notifications

## ADDED: StatusController notification management endpoints

### Requirement: Admin can read notification settings

Given an authenticated global admin user
When they send `GET /api/v2/notifications/settings`
Then the response is 200 with a `NotificationSettingsResponse`:
- `configured_system_notification_message`: the `AppOptions.NotificationMessage` value (may be null)
- `system_notification`: the currently cached `SystemNotification` (may be null)

### Requirement: Non-admin cannot access admin notification endpoints

Given an authenticated non-admin user
When they send any request to `POST/DELETE /api/v2/notifications/*` or `GET /api/v2/notifications/settings`
Then the response is 403 Forbidden.

### Requirement: Admin can set a system notification

Given an authenticated global admin user
When they send `POST /api/v2/notifications/system` with body `{ "value": "Maintenance tonight" }`
Then:
- The system notification is stored in cache key `system-notification`
- A `SystemNotification` message is published to the message bus (when `publish=true`, the default)
- The response is 200 with the `SystemNotification` object

#### Scenario: Empty message is rejected

Given an authenticated global admin user
When they send `POST /api/v2/notifications/system` with body `{ "value": "" }`
Then the response is 400 Bad Request.

#### Scenario: Publish suppression

Given an authenticated global admin user
When they send `POST /api/v2/notifications/system?publish=false` with body `{ "value": "Silent" }`
Then:
- The system notification is stored in cache
- No message is published to the message bus
- The response is 200 with the `SystemNotification` object

### Requirement: Admin can clear a system notification

Given an authenticated global admin user
When they send `DELETE /api/v2/notifications/system`
Then:
- The `system-notification` cache key is removed
- A blank `SystemNotification` is published to the message bus (when `publish=true`, the default)
- The response is 200

#### Scenario: Clear without publish

Given an authenticated global admin user
When they send `DELETE /api/v2/notifications/system?publish=false`
Then:
- The `system-notification` cache key is removed
- No message is published to the message bus
- The response is 200

### Requirement: Admin can send a release notification

Given an authenticated global admin user
When they send `POST /api/v2/notifications/release` with body `{ "value": "v8.0 released" }` and optional `?critical=false`
Then:
- A `ReleaseNotification` is published to the message bus
- The response is 200 with the `ReleaseNotification` object

### Requirement: Admin can force-refresh all clients

Given an authenticated global admin user
When they send `POST /api/v2/notifications/force-refresh` with optional body `{ "value": "reason" }`
Then:
- A `ReleaseNotification` is published with `critical=true` and optional message
- The response is 200 with the `ReleaseNotification` object

## ADDED: Svelte notification banners (Svelte UI only)

### Requirement: System notification banner displays on page load

Given a system notification is persisted in cache
When a user loads any authenticated page in the Svelte app
Then a destructive/danger banner is displayed at the top of the layout with the notification message.

#### Scenario: Fallback to configured message

Given no system notification is persisted in cache
And `PUBLIC_SYSTEM_NOTIFICATION_MESSAGE` is set to "Scheduled maintenance"
When a user loads any authenticated page
Then a destructive/danger banner displays "Scheduled maintenance".

#### Scenario: No notification and no fallback

Given no system notification is persisted in cache
And `PUBLIC_SYSTEM_NOTIFICATION_MESSAGE` is empty
When a user loads any authenticated page
Then no system notification banner is displayed.

### Requirement: Realtime system notification updates via WebSocket

Given a user has the Svelte app open
When a `SystemNotification` WebSocket message is received with a non-empty message
Then the system notification banner updates to show the new message.

#### Scenario: Clear notification via WebSocket

Given a user has the Svelte app open with a system notification banner visible
When a `SystemNotification` WebSocket message is received with an empty/null message
Then the system notification banner is hidden (falls back to env var if configured).

### Requirement: Release notification banner displays on WebSocket message

Given a user has the Svelte app open
When a `ReleaseNotification` WebSocket message is received with `critical=false`
Then an info banner is displayed with the release message.

### Requirement: Critical release notification triggers page reload

Given a user has the Svelte app open
When a `ReleaseNotification` WebSocket message is received with `critical=true`
Then `window.location.reload()` is called immediately.

### Requirement: Notification banners use accessible markup

Given a system notification banner is displayed
Then it has `role="alert"` and `aria-live="assertive"`.

Given a release notification banner is displayed
Then it has `role="status"` and `aria-live="polite"`.

### Requirement: No unsafe HTML rendering

Given any notification message content
When it is rendered in the Svelte UI
Then it is rendered as plain text (no innerHTML or {@html}).

## ADDED: Svelte admin notifications page (Svelte UI only)

### Requirement: System → Notifications page exists for global admins

Given an authenticated global admin user
When they navigate to `/system/notifications`
Then they see the notification management page with:
- Current configured fallback message (read-only)
- Current persisted system notification (if any)
- Actions: Set system notification, Clear, Send release notification, Force refresh

### Requirement: Notifications page is hidden from non-admins

Given an authenticated non-admin user
When they view the system navigation
Then the "Notifications" link is not visible.

## HTTP test file updates

### Requirement: tests/http files updated for notification endpoints

Given the notification endpoints exist in StatusController
Then `tests/http/status.http` includes sample requests for:
- GET notifications/settings
- POST notifications/system
- DELETE notifications/system
- POST notifications/release
- POST notifications/force-refresh

