# Playwright E2E Tests

These tests protect user-visible release workflows. They complement backend integration tests instead of repeating endpoint and repository assertions.

## Test Boundary

- Use the API to arrange isolated data, wait for asynchronous ingestion, and clean up.
- Perform the behavior under test through the browser.
- Assert navigation, rendered state, validation, dialogs, filters, and state that remains visible after reload.
- Leave response schemas, authorization matrices, query correctness, and business-rule permutations to integration tests.

## Maintenance Rules

- Prefer roles, labels, and visible text. Add a test ID only when the UI has no useful accessible contract.
- Do not use fixed sleeps. Wait for a visible outcome, URL, or the specific mutation response that unlocks the next UI assertion.
- Keep each spec centered on one user outcome. Seed prerequisites directly instead of replaying unrelated UI flows.
- Put reusable data setup in `support/event-data.ts` and fixture lifecycle in `fixtures/e2e-test.ts`.
- Add small task helpers when an interaction repeats. Do not grow a page object that mirrors every page or hides the user behavior.
- Tests must be independent and safe to retry. Every created project, organization, and user needs deterministic cleanup.

## Coverage Shape

- Authentication: validation, successful login, session restoration, logout, and protected-route redirect.
- Discovery: sidebar navigation, filters, empty states, and recovery.
- Investigation: open event and stack details through rendered tables and sheets.
- Triage: mutate stack state and verify the persisted UI after reload.
- Scoping: select and clear projects through the filter UI.
- Onboarding: sign up, create an organization/project, and verify setup instructions.
