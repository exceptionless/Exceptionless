# Proposal: Add Svelte Notifications Management

## Summary

Implement global notification support in the Svelte 5 UI matching the legacy Angular behavior: system notification banners, release notification banners, critical release force-refresh, configuration fallback messages, and a global-admin notification management page.

Additionally, add dedicated admin endpoints under `/api/v2/admin/notifications` to provide a cleaner management interface while preserving the existing `StatusController` notification endpoints for backwards compatibility.

## Classification

- **Type:** Feature + UI migration
- **Affected areas:** Backend/API, Svelte UI, Redis (cache key `system-notification`), WebSocket messages, tests
- **OpenSpec justification:** New API endpoints, WebSocket message consumption in new UI, admin authorization, cross-cutting UI/API contract, Angular-to-Svelte behavior migration

## Compatibility Risks

| Risk | Mitigation |
|------|-----------|
| Existing `StatusController` notification endpoints | Preserved unchanged; new admin endpoints are additive |
| WebSocket message format (`SystemNotification`, `ReleaseNotification`) | Consumed as-is; no format changes |
| Redis cache key `system-notification` | Shared between StatusController and new AdminController endpoints via extracted service |
| Config key `PUBLIC_SYSTEM_NOTIFICATION_MESSAGE` | Read-only consumption; no changes to how it's set |
| SDK/client expectations | No SDK changes; WebSocket messages unchanged |

## Rollback Plan

- New admin endpoints are additive; removing them has no impact on existing clients.
- Frontend notification banners degrade gracefully (no banner shown if fetch fails).
- Admin page is behind global-admin nav guard; removing it is safe.
