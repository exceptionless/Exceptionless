# Test runtime and CI sharding

The normal build targets a roughly **10-minute critical path**. Track elapsed workflow time, including setup and report jobs, rather than adding the durations of parallel jobs. More shards consume more runner minutes; increase their count only after inspecting timing reports.

## Execution

- `.NET`: four isolated runners build the suite and discover tests through Microsoft Testing Platform's JSON discovery. `backend-shards.mjs` groups whole classes, then distributes the longest classes first using `backend-durations.json`. New classes are included automatically; stale timing entries cannot select or exclude tests. Each runner retains the existing six collection workers and coverage collection.
- CI tooling uses the project's Node.js runtime (Node 24 in CI) and `node:test`. `tests/ci/package-lock.json` pins the small `saxes` XML parser and its `xmlchars` dependency for strict coverage XML parsing. Backend summaries read xUnit's native CTRF JSON output; TRX reports remain available as artifacts.
- Browser: six isolated Aspire applications run Playwright's native `--shard=N/6` with `fullyParallel` enabled for sharding. Each runner uses **one worker** because scenarios can change the shared administrator's preferences. This splits large spec files without introducing concurrent mutations of that user. Shard IDs are included in generated data names.
- Frontend unit/component tests actually execute in `test-client`, with two Vitest workers to bound memory. Previously the workflow only echoed the command.
- `test-api` and `test-e2e` remain the required checks. They depend on every shard, reject missing/duplicate/inconsistent results, and fail if any shard fails, is cancelled, or is skipped. `fail-fast: false` retains diagnostics from the other shards.
- .NET and E2E backend coverage are merged from Microsoft native coverage data before generating line reports with ReportGenerator. `test-api` retains the .NET-only report; `test-e2e` validates both suites and publishes the combined backend headline. Browser blob reports are merged into one HTML/JSON/JUnit report. Per-shard native coverage, TRX, CTRF JSON, JUnit, failure traces/screenshots, and Aspire logs remain downloadable for 14 days.
- E2E startup builds the Release AppHost and its referenced applications once. It does not build the full solution or the unused .NET test assembly.
- Browser shards use GitHub's Ubuntu 24.04 image (with its installed browser libraries) and install only Playwright's headless Chromium shell. This avoids unrelated apt upgrades and font downloads that added several minutes to individual runners. Browser versions still come from the locked Playwright package; all layout/locale tests remain enabled.
- Backend fixtures request `--test-services`, starting only Elasticsearch. Their queues/cache are in memory and storage is scoped to local files. The general `--services-only` mode still starts all development services. This also prevents backend tests from replacing a live preview's shared Azurite container with different dynamic ports.
- `--ci-e2e` uses temporary Redis and storage containers with allocated ports. This prevents concurrent local AppHosts from replacing shared containers or changing credentials/TLS configuration during a test run. Normal development retains its persistent containers and volumes.
- Superseded PR runs are cancelled; push/tag runs are not interrupted by newer runs.

Playwright's unsharded local and synthetic-monitoring configurations retain their existing reports, retry settings, and scheduling. CI rejects flaky passes with Playwright's `failOnFlakyTests` and the aggregate result gate; retries collect diagnostics but cannot turn a flaky build green. Traces capture the first failing attempt. All development verification uses localhost.

## Stability

The chaos tests measure requests with controlled notifications while retaining real WebSocket connections. Background jobs can publish delayed seed-data notifications, so the request-budget scenarios intercept those messages and inject explicit bursts. The existing event-visibility journey separately verifies that a real server push updates the visible list without navigation or manual refresh.

Thirty hide/resume cycles can complete different numbers of WebSocket handshakes on different machines. Each completed reconnect legitimately refreshes active queries. Visibility assertions therefore enforce at most one fetch per observed reconnect and at most one reconnect per resume, with a bounded observation window for late repeated work. Navigation has a separate request budget and waits for each URL transition. These checks still detect repeated listeners, query loops, and excess requests without assuming a particular handshake speed.

The chart refresh scenario waits for the initial list request and loading indicator before holding a refresh response. Its interception assertion has a bounded timeout instead of waiting until the entire test expires.

The API-key/first-event scenario also exposed a backend race during cleanup: the first-event configuration job could save an old project snapshot after deletion, making the project accessible again. The job now patches only its configuration flag. A deterministic two-case regression verifies that intervening project edits and soft deletion survive; the browser cleanup assertion remains intact.

## Timing reports and rebalancing

The aggregate checks show the slowest .NET classes/tests and browser tests (including retry cost) in the Actions job summary. Download `api-coverage` for merged coverage and freshly measured class durations. After a material suite change, review those durations and copy the JSON into `backend-durations.json`; timing data changes balance only. The checked-in weights come from the first hosted sharded run; refresh them as expensive classes or runner behavior change.

Example local commands from the repository root (PowerShell):

```powershell
dotnet build Exceptionless.slnx --configuration Release
npm ci --prefix tests/ci
dotnet tool restore
node tests/ci/backend-shards.mjs run --index 1 --count 4 --output "$env:TEMP/api-shard-1" --coverage
node --test tests/ci/shards.test.mjs tests/ci/backend-coverage.test.mjs
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

## E2E backend coverage (Phase 1)

Coverage is opt-in through `backend-shards.mjs --coverage` and `e2e-backend-coverage.mjs`. Ordinary `aspire run`, frontend development, and production builds do not enable profiling or change assemblies on disk. The collector uses dynamic process instrumentation; there is no static instrumentation for a later build to overwrite.

The local tool manifest pins `dotnet-coverage` **18.11.2**, matching the existing `Microsoft.Testing.Extensions.CodeCoverage` package. Both NuGet packages identify upstream commit `be7bc8830b6e477b1f66d951a326edfe2994abb5`. ReportGenerator remains at 5.5.11. Collection includes AppHost, Core, Insulation, Web, and now **Job**. The local Release proof adds 220 Job source lines to the denominator, so these figures should not be compared directly with historical reports that excluded it. Shared and linked source files are counted once in the line denominator; IL blocks belong to their compiled modules.

The historical `api-coverage` Cobertura report counted 34,868 class/line entries, representing 32,685 unique file/line locations. Canonical reporting removes those 2,183 duplicates and adds the 220 Job lines, yielding 32,905 unique source lines. This normalization is another denominator change, not missing instrumentation.

### Lifecycle and local commands

Install both client lockfiles and the existing Playwright Chromium shell before running the wrapper. It starts its own AppHost, discovers and probes localhost endpoints, waits for healthy API and Jobs processes, runs the existing browser tests once, snapshots while the applications are alive, stops only this checkout's AppHost, and explicitly shuts down the collector. Each invocation needs a new output directory. Session IDs include run, attempt, shard, and a random suffix.

```powershell
npm ci --prefix tests/ci
dotnet tool restore
npm ci --prefix src/Exceptionless.Web/ClientApp
npm ci --prefix src/Exceptionless.Web/ClientApp.angular

# One existing ingestion journey, no browser retries:
node tests/ci/e2e-backend-coverage.mjs --output "$env:TEMP/e2e-proof" --index 1 --count 1 -- e2e/tests/project-api-key-configuration.e2e.ts --retries=0

# One of the six normal browser shards:
node tests/ci/e2e-backend-coverage.mjs --output "$env:TEMP/e2e-shard-1" --index 1 --count 6

# Exercise the real collector's complementary-execution union:
node tests/ci/check-coverage-union.mjs
```

On codesmith, `$env:TEMP`/`TMPDIR` must point to `/home/ejsmith/tmp`. Use an isolated worktree when a preview is already running. The runner refuses to reuse an active AppHost in its checkout and never runs `aspire stop --all`.

Aspire 13.6's CLI startup chooses Debug even when MSBuild's `Configuration` environment property creates Release output. Its `dotnet run` hook also delegates to that CLI path. The wrapper therefore runs `dotnet run --configuration Release` with the SDK's `ASPIRE_SUPPRESS_CLI_RUN_HOOK=true` setting. It first runs `aspire certs trust --non-interactive`, preserving the CLI preparation needed on fresh runners. This builds only the AppHost graph and launches the actual Release API/Jobs outputs. The aggregator checks module identities, block denominators, and PDB SHA-256 source checksums; Debug and Release reports cannot silently mix.

On failure, the runner captures API, Jobs, App, and OldApp logs before shutdown and preserves the snapshot, final native report when available, collector diagnostics, and `lifecycle.json`. Test exit codes take precedence; snapshot, shutdown, conversion, and source-validation failures also fail the run. An unsuccessful collector never receives a complete manifest. `failOnFlakyTests`, assertions, discovery, and shard isolation are unchanged.

If API is running but Jobs/App remain waiting for API health, check certificate trust and the saved resource-state summary before changing timeouts. The first hosted probe exposed the missing CLI trust preparation that an already-trusted local machine hid. The collector output format is explicitly `coverage`; its implicit default can log an `Invalid outputType: default` error even while producing a file. Collector error-level diagnostics fail collection instead of being ignored.

Both .NET and browser runners install the same pinned Aspire CLI before building. Aspire embeds discovered CLI/bundle paths in AppHost assembly metadata: installing it on only the browser runners produced different AppHost module IDs despite identical source and Release configuration. The aggregate rejected that hosted probe. Keep the installation consistent; do not bypass the module-identity check.

### Reports, completeness, and limitations

The `api-coverage` artifact retains .NET-only coverage and measured test timings. The final `backend-coverage` artifact contains `dotnet/`, `e2e/`, and `combined/`, each with native `.coverage`, Microsoft XML, canonical source-line Cobertura, HTML, and JSON summaries. `e2e-added-lines.json` lists the exact source locations covered by browser execution but not by the .NET suite. The PR comment and final job summary label all three components.

Native reports are merged as a union, never by averaging percentages. XML parsing rejects malformed inputs. Manifests and native-file hashes identify the checkout commit, workflow run/attempt, shard/count, collection session, collector version, Release configuration, settings hash, and completion. The final aggregate re-converts the native input, checks source checksums against its checkout, and independently verifies that the merged source-line map equals the union of all inputs. Missing, duplicate, stale, modified, or incomplete inputs fail aggregation. Rerun **all jobs** for a fresh attempt; mixing artifacts from earlier attempts is intentionally rejected.

**Branch limitation:** Microsoft's native `coverage`/`xml` formats retain IL block coverage, not branch coverage. Its Cobertura output represents a condition as an aggregate percentage, which cannot distinguish two executions covering the same half from executions covering opposite halves. Converting native data back to Cobertura can even report `branch-rate="1"` without any conditions. This implementation does not publish that value. Reports show exact source-line and IL-block unions and explicitly mark exact cross-shard branch coverage unavailable. IL blocks are not a substitute name for branches. The canonical Cobertura reports intentionally omit branch attributes.

Untouched methods in the collector's instrumented modules remain in the denominator. This does not claim coverage of assemblies that are never loaded. Backend startup and seed-data execution are included, as they are in an instrumented process run. Frontend JavaScript, Svelte, CSS, and legacy Angular coverage are outside Phase 1.

Download and validate a complete hosted run (PowerShell):

```powershell
$coverageInput = Join-Path $env:TEMP 'backend-coverage-input'
gh run download <run-id> --pattern 'api-results-<run-id>-<attempt>-*' --dir "$coverageInput/api"
gh run download <run-id> --pattern 'e2e-backend-<run-id>-<attempt>-*' --dir "$coverageInput/e2e"
$env:GITHUB_RUN_ID = '<run-id>'
$env:GITHUB_RUN_ATTEMPT = '<attempt>'
# Run from the exact checkout commit recorded in the manifests.
node tests/ci/backend-coverage.mjs aggregate $coverageInput --output "$env:TEMP/backend-coverage-report" --api 4 --e2e 6
```

### Proof and cost

The existing first-event/API-key journey passed locally with retries disabled. Before the journey, `EventPostsJob.ProcessQueueEntryAsync` had zero covered lines; afterward it had 68 fully covered lines and one partially covered line in the initial Debug propagation experiment. `EventEndpoints.SubmitEventByPostAsync` was also exercised. Separate API and Jobs processes were enabled throughout (`RunJobsInProcess=false`). The corrected Release run's module IDs and PDB checksums matched the existing Release .NET suite.

A warm local Release proof took 25.4 seconds: 13.8 seconds startup, 8.5 seconds browser execution, and 3.1 seconds snapshot/shutdown. Sampled peak process-tree RSS was about 2.6 GiB. This is one journey on codesmith, not a hosted runtime estimate or an overhead comparison. `lifecycle.json` records per-run startup/test/shutdown timings and peak process-tree/host memory; host memory on a shared development machine includes other workloads. Native conversion and aggregation timings are reported separately. Hosted critical-path, runner-minute, and two-consecutive-final-commit evidence must be recorded before this phase is considered validated.

The complete local .NET suite passed 3,083 tests with three existing skips. Merging its four shards with the single ingestion journey passed module/source validation and the independent source-union assertion. The .NET-only report covered 26,299/32,905 canonical lines (79.92%); the combined report covered 27,006 (82.07%). The journey added 707 lines, including 10 in `EventHandler`, 69 in `EventNotificationsJob`, and WebSocket message delivery paths. A substantial portion is startup/wiring (153 AppHost and 123 Job `Program.cs` lines), so the increase should not be described entirely as new business-logic coverage. Aggregation took 10.8 seconds with 313 MiB Node peak RSS. HTML generation reproduced the canonical line totals and reported zero branch records, rather than a fabricated branch percentage.

The historical final sharding run [36908874514](https://github.com/exceptionless/Exceptionless/actions/runs/36908874514) had a 500-second job critical path (about 8m 22s including workflow overhead) and 71.4 summed runner minutes. Use the same completed-job timestamps to compare hosted runs of this change.

The first complete green coverage run, [36916411343](https://github.com/exceptionless/Exceptionless/actions/runs/36916411343) at `f8ec4f5df`, passed 3,083 .NET tests (three existing skips), 960 frontend tests, and all 99 E2E tests with zero retries. All ten native reports passed revision, module, checksum, completion, and union checks:

| Execution | Covered source lines | Covered IL blocks |
| --- | ---: | ---: |
| .NET tests | 26,249/32,905 (79.77%) | 61,336/80,091 (76.58%) |
| E2E backend | 14,720/32,905 (44.73%) | 31,609/80,091 (39.47%) |
| Combined | 27,115/32,905 (82.40%) | 63,554/80,091 (79.35%) |

This adds 866 lines beyond the .NET suite: for example, 71 in `EventNotificationsJob`, 22 in `SavedViewHandler`, and 16 in `EventHandler`. It also adds startup/wiring execution, including 139 AppHost and 123 Job `Program.cs` lines. The hosted Release data confirms ingestion itself: shard 1 covered 16 IL blocks in `SubmitEventByPostAsync` and 134 blocks/59 lines in `EventPostsJob.ProcessQueueEntryAsync`. Counts can vary slightly between complete runs because background work also executes during collection.

| Measurement | Historical baseline | First complete coverage run |
| --- | ---: | ---: |
| Workflow elapsed | 8m 22s | 10m 43s |
| Job critical path | 500s | 641s |
| Summed executed runner minutes | 71.4 | 77.0 |
| Sum of six E2E startup times | 635s | 778s |
| Sum of six E2E test execution times | 1,381s | 1,501s |
| Sum of six shutdown times | about 5s | 13.5s |
| Final aggregate job | 27s | 67s |

Coverage validation/native merging took 13.4 seconds and 496 MiB Node peak RSS; validation, merging, and all three HTML reports occupied a 16-second step. Browser-shard process-tree RSS peaks ranged from 4,100–4,744 MiB, with host used-memory peaks of 9,268–9,896 MiB. RSS samples include descendant build/application/browser processes but not Docker container processes; host used memory includes containers and runner services. The baseline did not collect equivalent memory samples, so these are absolute peaks, not a claimed memory-overhead delta.

The 10m 43s characterization run exceeded the roughly ten-minute goal. Shard 5 was the critical path: 147.2s startup, 308.0s tests, and 2.2s shutdown. Its test slowdown was spread across scenarios; there were no retries or isolated timeout outliers. The earlier coverage probe ran that same shard in 95.6s startup/245.2s tests, so this comparison alone cannot separate instrumentation cost from runner variation. To reduce a measured avoidable cost, the real collector union contract check now runs once on .NET shard 1, parallel with the browser shards, instead of delaying final aggregation (that check took 32s in the earlier probe). The final-commit repeated-run evidence and resulting elapsed/runner-minute measurements are recorded in the PR; the figures above identify the measured characterization commit and do not claim to be those final runs.

References: [Microsoft collector lifecycle and merging](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-coverage), [native formats versus branches](https://github.com/microsoft/codecoverage/issues/147), [Cobertura merge ambiguity](https://github.com/microsoft/codecoverage/issues/11).
## Frontend coverage (Phase 2)

Frontend coverage is separate from backend coverage. `test-client` runs the existing
Vitest projects once with the pinned Istanbul provider; each existing Chromium E2E
shard collects the opted-in Vite instrumentation during its normal test run.
`test-e2e` waits for all three suites and publishes unit/component, E2E browser, and
combined frontend line reports in HTML, JSON, and Cobertura. Separate unit/browser
collector reports retain their branch details. The PR comment shows the
backend and frontend figures separately. No minimum percentages are imposed.

The frontend denominator includes tracked application `.ts`, `.js`, and `.svelte`
files under `ClientApp/src`, including untouched files. Tests, stories, declarations,
generated API/schema output, dependencies, legacy Angular, and CSS are excluded.
Files containing only erased types, imports, and re-exports are retained in the
inventory with no executable counters. Missing executable files fail aggregation.
Server-only SvelteKit files (`+server`, `.server`, and `/server/`) belong to the
unit/combined report, not browser coverage.

Both collectors use `@vitest/coverage-istanbul` 5.0.3's instrumenter, pinned to 1.0.2.
The prototype compared V8 first: when `JSON.parse` throws, V8 marked the following
return statement covered, while Istanbul correctly left it uncovered. The focused
collector contract preserves that case and verifies original TypeScript/Svelte
locations, opposite branch arms, and untouched-file inclusion. The Vite adapter
explicitly handles `.svelte` and excludes CSS/SSR virtual modules. It runs only in
the development server when `E2E_FRONTEND_COVERAGE_DIRECTORY` is set; ordinary
development, production builds, and synthetic monitoring do not enable it.

### Collection and source mapping

The coverage-aware Playwright browser fixture observes all contexts and pages,
including manually created contexts, popups, and early page closes. It snapshots
before navigation/unload, on `pagehide`, and before explicit page/context teardown.
Chromium protocol bindings serialize reports before their document disappears.
Instrumented documents register themselves; a missing final snapshot fails the
worker. Crashes, test failures, and collector errors retain available raw data but
cannot yield a successful complete aggregate. Code running only after the final
unload snapshot is outside that snapshot; this is not browser process tracing.

Source line coverage follows Istanbul's original mapped statement starting lines.
Svelte can reorder template expressions, so mapped range endpoints are preserved,
not sorted into invented source spans. Unmapped compiler-generated control flow
(including some template decisions) is not represented as original-source branch
coverage. Each component's branch figure is explicitly **mapped branches**, not a claim to
measure every Svelte compiler decision. An implicit `if` false arm keeps its own
counter even when it has no explicit `else` source span.

Svelte's server-side unit compilation and browser compilation emit different
statement and branch maps. The combined report therefore uses the exact union of
covered original source lines and explicitly marks **combined branch coverage
unavailable**. Unit and browser branch figures remain separate; their denominators
are not directly comparable. Canonical HTML/Cobertura reports contain no branch
records. The `unit/collector/` and `e2e/collector/` reports retain their respective
compiler-specific maps, so their line denominators can differ from the canonical
line reports. HTML totals are checked against the canonical union before publication.
Coverage class names retain file extensions so TypeScript and Svelte files with
the same basename cannot be conflated.

Route warmup is disabled during Vitest runs. With an empty Vite cache, warmup can
compile and cache components before the coverage provider initializes, silently
losing their counters. The existing navigation-command and source-map-page tests
reproduced this: all assertions passed, but 263 exercised source lines were missing.
Disabling warmup preserves the tests' compilation modes and records those hits on
the first cold run.
Browser coverage also disables broad route warmup so each shard compiles only
the routes it visits. The separate browser source plan still includes every
eligible untouched file in the denominator.

After the existing unit run, Vite compiles every browser-eligible file through the
same instrumentation plugin without executing application modules. This produces
the zero-hit `browser-plan.json`, including untouched browser source. Its statement,
function-body, and ordered branch maps must agree exactly with every E2E shard.
Canonical unit and combined line reports share the union of server/unit and browser
source locations; browser reports exclude server-only files. Counters are reduced
to covered/uncovered unions, never averaged percentages.

The initial and final source inventory, lock/config fingerprint, commit, run,
attempt, session, shard count/index, worker completion, source-plan hash, and
artifact checksum are checked. Missing, duplicate, incompatible, stale, malformed,
or incomplete inputs fail aggregation. In `--ci-e2e` mode, Aspire skips its implicit
dependency installation: CI and local callers install locked dependencies before
startup. This prevents npm from rewriting the lockfile during collection.

### Local frontend collection

Use a fresh output directory for every run. Start from the repository root; the
existing Aspire prerequisites and localhost restrictions above still apply.

```powershell
npm ci --prefix src/Exceptionless.Web/ClientApp
npm ci --prefix src/Exceptionless.Web/ClientApp.angular
npm ci --prefix tests/ci
node --test tests/ci/frontend-coverage.test.mjs
node tests/ci/check-frontend-coverage.mjs

node tests/ci/frontend-unit-coverage.mjs --output "$env:TEMP/frontend-unit-run" -- --maxWorkers=2
node tests/ci/e2e-backend-coverage.mjs --output "$env:TEMP/backend-run" --frontend-output "$env:TEMP/frontend-browser-run" --index 1 --count 1 -- e2e/tests/event-visibility.e2e.ts --retries=0
```

On codesmith, `TEMP`/`TMPDIR` must point under `/home/ejsmith/tmp`. To aggregate,
place the unit artifact directory under `INPUT/unit/` and all browser artifact
directories under `INPUT/e2e/`, then run
`node tests/ci/frontend-coverage.mjs aggregate INPUT OUTPUT --count N`.
All inputs must have matching collection identity and unchanged source/config.
`e2e-added-lines.json` lists source lines covered only by browser execution.
Generate each canonical HTML report with the pinned ReportGenerator, for example
`dotnet tool run reportgenerator -- "-reports:OUTPUT/combined/Cobertura.xml" "-targetdir:OUTPUT/combined/html" "-reporttypes:Html;JsonSummary"`.
After generating all three reports, run
`node tests/ci/frontend-coverage.mjs verify-html OUTPUT`.

If a document fails to finalize, inspect its worker artifact and the Playwright
failure report; do not accept retries or omit the worker. If source maps differ,
inspect the affected original source and collector outputs instead of merging
their percentages. `collection-start.json`, `collection-end.json`, worker JSON, and `collection-error.txt`
are diagnostic artifacts, not substitutes for a complete manifest.

A complete local run of all 960 existing unit/component tests covered 5,814/21,692
lines (26.80%) in the unit compiler's footprint and 3,291/11,908 mapped branch arms
(27.63%), in 62.86 seconds with two workers. The browser source plan contributes
260 additional executable source lines to the canonical denominator: the comparable
unit-only baseline is 5,814/21,952 (26.49%). This denominator change does not add
covered lines. The local browser-plan prototype took 23.0 seconds and 2.02 GiB peak
Node RSS; it compiles sources without rerunning tests. The source inventory
contained 864 files, including 70 files with no unit executable counters.
The focused browser contract finalized seven documents across
reload, full navigation, popup closure, manual contexts, and early page teardown.
Hosted combined figures, resource measurements, overhead, and exact final-commit
run links are recorded in [Phase 2 PR #2620](https://github.com/exceptionless/Exceptionless/pull/2620);
local unit numbers are not a hosted
E2E or performance result.

Hosted characterization run [36930609850](https://github.com/exceptionless/Exceptionless/actions/runs/36930609850)
at `f235bf87b` passed all .NET/frontend test jobs and all 99 browser tests with zero
retries. Its final report job rejected an incorrectly flattened unit-artifact
directory; the download now names that directory explicitly. Local reaggregation
of all seven complete, matching frontend artifacts passed source-map, revision,
union, and HTML-total validation:

| Execution | Canonical source lines |
| --- | ---: |
| Unit/component | 5,815/21,952 (26.49%) |
| E2E browser | 11,939/21,952 (54.39%) |
| Combined | 13,439/21,952 (61.22%) |

E2E added 7,624 lines, including 439 in `use-saved-views.svelte.ts`, 347 in the
Events page, and 310 in the Sessions page. These are execution contributions,
not a claim that every behavior on those lines has assertions.

That characterization consumed 11m 24s and 85.75 summed runner minutes before the
report failure; it is not a successful-build timing. Across the six browser shards,
startup totaled 903s, test execution 1,770s, and shutdown 13.5s. Process-tree RSS
peaks were 4,393–4,848 MiB; host used-memory peaks were 9,922–11,658 MiB.
The frontend unit command took 112.0s plus 39.0s finalization (38.5s for the source
plan), with 1,724 MiB collector-helper peak RSS. Broad route warmup was subsequently
disabled for browser coverage to avoid instrumenting unvisited routes in every
shard. Final successful-build overhead and the two final-commit hosted runs are
recorded in PR #2620; baseline memory telemetry is insufficient to claim a precise
memory-overhead delta.
