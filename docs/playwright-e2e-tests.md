# Playwright E2E Tests

The ClientApp Playwright tests validate the core Exceptionless lifecycle against either the local Aspire stack or the production site:

1. Log in with a dedicated test account.
2. Create an isolated organization.
3. Create a project in that organization.
4. Submit a real event through the public API.
5. Verify the event through API and Svelte UI surfaces.
6. Delete the temporary organization during fixture teardown.

## Local Run

Start the full app stack first:

```powershell
aspire run
```

Then run the E2E tests from the Svelte app folder:

```powershell
cd src/Exceptionless.Web/ClientApp
npm run test:e2e:local
```

Local defaults target `https://web-ex.dev.localhost:7131` for the app and derive API calls from that origin at `/api/v2`. The default local credentials are `admin@exceptionless.test` / `tester`.

## Environment Variables

| Name           | Description                                                               |
| -------------- | ------------------------------------------------------------------------- |
| `E2E_APP_URL`  | Base URL for the app origin. API requests use this origin plus `/api/v2`. |
| `E2E_EMAIL`    | Test account email address.                                               |
| `E2E_PASSWORD` | Test account password.                                                    |
| `E2E_RUN_ID`   | Optional identifier included in generated org, project, and event names.  |
| `E2E_ENV`      | Set to `production` to require explicit URL and credential variables.     |

## Production Nightly Policy

Production E2E tests must use a dedicated service account stored in GitHub secrets. Each run creates one temporary organization and deletes it with the public organization delete API during teardown. Tokens and passwords must never be printed to logs or committed to the repository.

## Test Style

- Prefer user-visible locators such as `getByRole`, `getByLabel`, and `getByText`.
- Use Playwright fixtures for setup and cleanup so resources are removed even when assertions fail.
- Keep the org/project/event journey self-contained instead of splitting it into dependent tests.
- Use Playwright `request` for API setup and polling, and browser assertions for user-visible behavior.
- Use SvelteKit's current Playwright test filename convention: `*.e2e.{ts,js}`.
- Keep CI stable with Chromium-only E2E runs, retries, one worker, and traces on first retry.

## SvelteKit Integration

SvelteKit's current `sv add playwright` template adds `@playwright/test`, a `test:e2e` script, a `playwright.config.ts`, and test files that match `*.e2e.{ts,js}`. The template normally starts a built preview server with `npm run build && npm run preview`, but these tests intentionally use the Aspire AppHost instead because the Exceptionless UI depends on the API, Elasticsearch, Redis, and the job worker. CI installs Aspire CLI 13.3, starts AppHost with `aspire run --detach`, waits for `Api`, `Jobs`, and `App` with `aspire wait`, stops with `aspire stop`, and all E2E runs point Playwright and API setup at a single `E2E_APP_URL` origin.
