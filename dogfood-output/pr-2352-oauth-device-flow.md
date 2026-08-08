# PR #2352 OAuth device-flow local dogfood

Date: 2026-08-05
Checkout: `/Users/blake/.codex/worktrees/431d/Exceptionless`
Review head before the final frontend fix: `92a1d0021` (merge-forward of `origin/main`)

## Static and focused proof

- `dotnet build` — passed, 0 warnings, 0 errors.
- `dotnet test --project tests/Exceptionless.Tests -- --filter-class Exceptionless.Tests.Services.OAuthDeviceServiceTests` — passed, 1/1.
- `npm ci` — passed; npm reported blocked optional install scripts for `esbuild` and `fsevents`.
- `npm run test:unit -- --run src/lib/features/auth/oauth.test.ts` — passed, 7/7 after correcting the device page fetch-client import.
- `npm run check` — passed, 0 Svelte errors and 0 warnings after correcting the device page fetch-client import.

## Runtime dogfood gate

Attempted `aspire run` from the repository root. The AppHost built and printed a dashboard URL, but the runtime surface did not become reachable.

- `aspire describe --apphost src/Exceptionless.AppHost --format Json --non-interactive` — blocked by `UnauthorizedAccessException` writing `/Users/blake/.aspire/logs/...`.
- `curl -k https://api-ex.dev.localhost:7111/api/v2/about` — connection failed, HTTP `000`.
- `curl http://api-ex.dev.localhost:7110/api/v2/about` — connection failed, HTTP `000`.
- `curl -k https://web-ex.dev.localhost:7131/next/oauth/device` — connection failed, HTTP `000`.
- `curl -k https://localhost:<AppHost-dashboard-port>/login` — connection failed after the CLI printed the dashboard URL.
- The focused OAuth integration class independently failed before test execution with `Aspire.Hosting.DistributedApplicationException`: Docker was found but unhealthy. All 86 tests failed at shared `AppWebHostFactory` initialization; 0 test bodies ran.

No browser or authenticated device-consent workflow was claimed because the local API and Svelte endpoints never became reachable. The AppHost was stopped cleanly with Ctrl-C after the single startup attempt.
