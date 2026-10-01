# Test runtime and CI sharding

The normal build targets a roughly **10-minute critical path**. Track elapsed workflow time, including setup and report jobs, rather than adding the durations of parallel jobs. More shards consume more runner minutes; increase their count only after inspecting timing reports.

## Execution

- `.NET`: four isolated runners build the suite and discover tests through Microsoft Testing Platform's JSON discovery. `backend-shards.mjs` groups whole classes, then distributes the longest classes first using `backend-durations.json`. New classes are included automatically; stale timing entries cannot select or exclude tests. Each runner retains the existing six collection workers and coverage collection.
- CI tooling uses the project's Node.js runtime (Node 24 in CI), built-in modules, and `node:test`, with no additional packages. Backend summaries read xUnit's native CTRF JSON output; TRX reports remain available as artifacts.
- Browser: six isolated Aspire applications run Playwright's native `--shard=N/6` with `fullyParallel` enabled for sharding. Each runner uses **one worker** because scenarios can change the shared administrator's preferences. This splits large spec files without introducing concurrent mutations of that user. Shard IDs are included in generated data names.
- Frontend unit/component tests actually execute in `test-client`, with two Vitest workers to bound memory. Previously the workflow only echoed the command.
- `test-api` and `test-e2e` remain the required checks. They depend on every shard, reject missing/duplicate/inconsistent results, and fail if any shard fails, is cancelled, or is skipped. `fail-fast: false` retains diagnostics from the other shards.
- .NET coverage is merged as a union with ReportGenerator before publishing the existing coverage summary. Browser blob reports are merged into one HTML/JSON/JUnit report. Per-shard TRX, CTRF JSON, JUnit, failure traces/screenshots, and Aspire logs remain downloadable for 14 days.
- E2E startup lets Aspire build its referenced applications once. The former Release solution build was unused by Aspire's Debug startup and also built the unused .NET test assembly.
- Browser shards use GitHub's Ubuntu 24.04 image (with its installed browser libraries) and install only Playwright's headless Chromium shell. This avoids unrelated apt upgrades and font downloads that added several minutes to individual runners. Browser versions still come from the locked Playwright package; all layout/locale tests remain enabled.
- Backend fixtures request `--test-services`, starting only Elasticsearch. Their queues/cache are in memory and storage is scoped to local files. The general `--services-only` mode still starts all development services. This also prevents backend tests from replacing a live preview's shared Azurite container with different dynamic ports.
- `--ci-e2e` uses temporary Redis and storage containers with allocated ports. This prevents concurrent local AppHosts from replacing shared containers or changing credentials/TLS configuration during a test run. Normal development retains its persistent containers and volumes.
- Superseded PR runs are cancelled; push/tag runs are not interrupted by newer runs.

Playwright's unsharded local and synthetic-monitoring configurations retain their existing reports, retry settings, and scheduling. CI rejects flaky passes with Playwright's `failOnFlakyTests` and the aggregate result gate; retries collect diagnostics but cannot turn a flaky build green. Traces capture the first failing attempt. All development verification uses localhost.

## Stability

The chaos tests measure requests with controlled notifications while retaining real WebSocket connections. Background jobs can publish delayed seed-data notifications, so the request-budget scenarios intercept those messages and inject explicit bursts. The existing event-visibility journey separately verifies that a real server push updates the visible list without navigation or manual refresh.

The navigation-cache scenario also intercepts server broadcasts. First-time saved-view creation legitimately invalidates those queries, so its notifications must not affect the scenario's unchanged-data request counts. Its navigation helper waits for saved-view groups instead of selecting the temporary direct links shown before the sidebar data loads. Saved-view invalidation and live updates retain separate coverage.

Thirty hide/resume cycles can complete different numbers of WebSocket handshakes on different machines. Each completed reconnect legitimately refreshes active queries. Visibility assertions therefore enforce at most one fetch per observed reconnect and at most one reconnect per resume, with a bounded observation window for late repeated work. Navigation has a separate request budget and waits for each URL transition. These checks still detect repeated listeners, query loops, and excess requests without assuming a particular handshake speed.

The chart refresh scenario waits for the initial list request and loading indicator before holding a refresh response. Its interception assertion has a bounded timeout instead of waiting until the entire test expires.

The API-key/first-event scenario also exposed a backend race during cleanup: the first-event configuration job could save an old project snapshot after deletion, making the project accessible again. The job now patches only its configuration flag. A deterministic two-case regression verifies that intervening project edits and soft deletion survive; the browser cleanup assertion remains intact.

## Timing reports and rebalancing

The aggregate checks show the slowest .NET classes/tests and browser tests (including retry cost) in the Actions job summary. Download `api-coverage` for merged coverage and freshly measured class durations. After a material suite change, review those durations and copy the JSON into `backend-durations.json`; timing data changes balance only. The checked-in weights come from the first hosted sharded run; refresh them as expensive classes or runner behavior change.

Example local commands from the repository root (PowerShell):

```powershell
dotnet build Exceptionless.slnx --configuration Release
node tests/ci/backend-shards.mjs run --index 1 --count 4 --output "$env:TEMP/api-shard-1" --coverage
node --test tests/ci/shards.test.mjs
```

Run local .NET shards **sequentially**: separate test processes on one host reuse test index scopes. Hosted shards are isolated machines. To validate or rebalance from downloaded artifacts:

```powershell
gh run download <run-id> --pattern 'api-results-*' --dir "$env:TEMP/api-results"
node tests/ci/backend-shards.mjs report "$env:TEMP/api-results" --count 4 --coverage --write-timings tests/ci/backend-durations.json
```

For a browser shard, start Aspire, discover and health-check its current local App URL, then from `src/Exceptionless.Web/ClientApp`:

```powershell
$env:E2E_URL = 'https://localhost:<discovered-port>'
$env:E2E_SHARD = '1'
npm run test:e2e:ci -- --shard=1/6
```

Keep the matrix counts, runner arguments, and aggregate completeness checks in sync when resizing either suite. The small CI-tool tests exercise lost/duplicated tests, inconsistent discovery, missing coverage, and failed/skipped browser results; they protect against a falsely green build.

## October 2026 audit

Baseline hosted run [36881017146](https://github.com/exceptionless/Exceptionless/actions/runs/36881017146), before these changes:

| Job | Elapsed | Test execution |
| --- | ---: | ---: |
| .NET | 13m 12s | 11m 16s |
| Browser | 25m 14s | 20m 47s |

The clean local baseline at `f4a7c61e5` passed 3,081 .NET tests (three existing skips) in 3m 52s with coverage, and all 957 frontend unit/component tests. Local timings are not estimates of hosted runtime.

Changes to test value/cost:

- Removed `UsageServiceTests.RunBenchmarkAsync`: 10,000 operations, logging only, no correctness assertions. Existing usage/limit/concurrency tests retain behavior coverage.
- Replaced `EventValidatorTests`' 10,000 repeated validations of the same object with one parsed-client-event compatibility check. Its other cases no longer parse that fixture during every constructor call.
- Removed the duplicate orphan-cleanup test from `CleanupDataJobTests`. The dedicated orphan-job suite retains preservation/deletion and tenant cases. Its partition case now uses 1,001 distinct missing stack IDs plus three valid events, crossing the real partition boundary with 1,004 records instead of 15,000.
- Consolidated eight additional cleanup tests into the existing tenant-aware scenarios. Retained multiple/new/already-suspended tokens, active tenant preservation, source-map/profile-image deletion (including already-deleted projects), stack cleanup, and retention hierarchy assertions. The healthy-data scenario retains the no-suspended-organizations case. Distinct OAuth, synthetic-account, usage-accounting, and mixed-plan retention contracts remain separate.
- Isolated heartbeat middleware settings from Elasticsearch; running the class early on a clean runner exposed its accidental database dependency. Fixed the readiness helper's observation of Foundatio's non-atomic startup-result publication.
- Removed the 30-second empty-queue wait in the project deletion endpoint test and asserted the actual synchronous soft-delete contract: no work items and the project no longer visible.
- Combined the five timestamp locale cases into one scenario sharing indexed data, retaining all five fresh locale/timezone browser contexts and both event/stack accessibility checks.
- Combined paired tour history/Search regressions at each checkpoint and retained their assertions. Folded the pure reference-ID check into the organization-switching scenario. Browser cases fall from 106 to 99 without dropping those checks.

Retained expensive coverage that tests distinct risks: the real pipeline payload corpus, OAuth grant-family pagination, cleanup/tenant boundaries, real event ingestion, and reactive event/stack chaos tests. Their size or duration alone is not a reason to delete them. The chaos tests' bounded observation windows detect repeated work and cannot all be replaced by an immediate assertion.

Agent guidance now requires a distinct regression rationale, reuse of existing scenarios, the cheapest reliable test layer, minimal boundary-sized fixtures, and attention to runtime. Test counts and coverage percentages alone do not establish value.

## Testing dependency review (2026-10-01)

Checked the resolved npm lockfile, NuGet packages (including transitive vulnerability scanning), stable package registries, and upstream release notes.

| Tooling | Version after review | Decision |
| --- | --- | --- |
| Playwright | 1.63.0 | Already latest stable; retain native sharding, blob reports, and isolated browser contexts. |
| xUnit v3 / Visual Studio adapter | 4.0.1 / 4.0.0 | Already latest stable; the framework package is named `xunit.v3.mtp-v2` despite its 4.x package version. |
| Aspire testing, .NET Test SDK, coverage, time-provider testing | 13.6.0 / 18.10.1 / 18.11.2 / 10.10.0 | Already latest stable; retain Microsoft Testing Platform and IDE support. |
| Vitest / jsdom | 5.0.3 / 30.1.1 | Updated for report, mock, Blob, focus, and DOM fixes. |
| Testing Library Svelte / jest-dom / WebSocket mock | 5.4.2 / 7.0.1 / 0.8.0 | Already latest stable. |
| Storybook packages / Svelte CSF / Chromatic addon | 10.6.1 / 5.1.5 / 5.4.0 | Updated the component and visual testing toolchain together. |
| Vite / Svelte Vite plugin | 8.3.2 / 7.3.1 | Updated the shared test/build host and plugin. |

Compatibility was checked against the packages' Node, Vite, Svelte, and Storybook peer requirements. CI uses Node 24; no preview packages or new script dependencies were introduced. The scripts consume native xUnit CTRF JSON and use the built-in Node test runner. The existing GitHub logger supports MTP, and the Visual Studio adapter remains useful for IDE discovery; neither is a redundant legacy package.

Release references: [Playwright](https://playwright.dev/docs/release-notes), [xUnit](https://xunit.net/releases/), [Vitest](https://github.com/vitest-dev/vitest/releases/tag/v5.0.3), [jsdom](https://github.com/jsdom/jsdom/releases/tag/v30.1.1), [Storybook](https://github.com/storybookjs/storybook/releases/tag/v10.6.1), [Svelte CSF](https://github.com/storybookjs/addon-svelte-csf/releases/tag/v5.1.5), [Chromatic addon](https://github.com/chromaui/addon-visual-tests/releases/tag/v5.4.0), [Vite](https://github.com/vitejs/vite/releases/tag/v8.3.2), and [Svelte Vite plugin](https://github.com/sveltejs/vite-plugin-svelte/releases/tag/@sveltejs%2Fvite-plugin-svelte@7.3.1).

The development dependency audit also identified vulnerable `brace-expansion` and `fast-uri` versions. Refreshing them to 5.0.12 and 3.1.8 keeps their consumers' supported major versions and clears those findings. A pre-existing [low-severity DOMPurify runtime advisory](https://github.com/advisories/GHSA-p98j-92pf-mc4p) remains outside this testing-toolchain change. NuGet reported no vulnerable packages in the test project's dependency graph.
