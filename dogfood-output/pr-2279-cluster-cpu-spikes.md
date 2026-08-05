# PR #2279 local verification

Date: 2026-08-05
Branch: `copilot/investigate-cluster-cpu-spikes`
Scope: recent-ingest orphan cleanup and cleanup-job health state

## Source and review

- PR was confirmed `OPEN`, non-draft, and `MERGEABLE` at head `5a53ae9c5d08405269dc55197070f79fcd9d089f` before the merge-forward.
- `origin/main` was 52 commits ahead of the PR branch, so it was merged with a normal merge commit and pushed as `f24d4f5f4`.
- `git diff origin/main...HEAD` contains only:
  - `src/Exceptionless.Core/Jobs/CleanupOrphanedDataJob.cs`
  - `tests/Exceptionless.Tests/Jobs/CleanupOrphanedDataJobTests.cs`
- `git diff --check origin/main...HEAD`: passed.
- Surgical review found no additional high-confidence defect requiring a source change; no unrelated files were modified.

## Build and tests

Passed:

```text
dotnet restore tests/Exceptionless.Tests/Exceptionless.Tests.csproj
dotnet build tests/Exceptionless.Tests/Exceptionless.Tests.csproj --no-restore --configuration Release -m:1
Build succeeded. 0 warnings, 0 errors.
```

The focused suite compiled and launched, but all 27 tests were blocked during the shared `AppWebHostFactory` fixture startup:

```text
dotnet test --no-restore --no-build --configuration Release --max-parallel-test-modules 1 -- --filter-class Exceptionless.Tests.Jobs.CleanupOrphanedDataJobTests
```

Result: 27 failed during fixture initialization, 0 test bodies executed. Aspire reported:
`Container runtime 'docker' was found but appears to be unhealthy.`

## Aspire and dogfood

- Initial `aspire run` hit `MSB4166` (parallel MSBuild child exited prematurely).
- A serial AppHost build passed:

  ```text
  dotnet build src/Exceptionless.AppHost/Exceptionless.AppHost.csproj --no-restore -m:1
  Build succeeded. 0 warnings, 0 errors.
  ```

- `aspire run --no-build --non-interactive` started a local dashboard, but endpoint discovery showed the backend API waiting for infrastructure.
- Aspire resource state: Elasticsearch, Redis, Mail, and Storage were `RuntimeUnhealthy`; API and Jobs were `Waiting` on those resources.
- API health and browser dogfood were not claimed because the API never became reachable. No production URL or data was used.

## Residual gates

- Local focused test execution and changed-surface runtime dogfood remain blocked by the unhealthy Docker runtime.
- The earlier green PR CI run `30577192830` passed API, client, E2E, Docker, and version jobs at the pre-merge head; the merge-forward push requires a fresh CI result for `f24d4f5f4`.
