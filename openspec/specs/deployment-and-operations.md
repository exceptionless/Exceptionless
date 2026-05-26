# Spec: Deployment and Operations

Baseline spec for Docker images, deployment topology, and operational concerns.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## Docker Images

The `Dockerfile` produces multiple targets:

| Target | Image | Contents |
|--------|-------|----------|
| `api` | `exceptionless/api` | API host only (no frontend assets) |
| `job` | `exceptionless/job` | Background job worker |
| `app` | `exceptionless/app` | API + frontend assets (Node build) |
| `exceptionless` | `exceptionless/exceptionless` | Fully self-contained: Elasticsearch + API + Jobs + frontend (tags only) |

Note: `exceptionless/ui` images are deprecated in favor of `exceptionless/app`.

## Build Process

1. .NET SDK image restores + builds both Web and Job projects.
2. `api-publish` — `dotnet publish` Web project with `SkipSpaPublish=true`.
3. `job-publish` — `dotnet publish` Job project.
4. `app-publish` — `dotnet publish` Web project with Node/frontend build included.
5. `exceptionless` — all-in-one based on custom Elasticsearch image + supervisord.

## Runtime Configuration

- `EX_ConnectionStrings__Storage` — file storage connection string.
- `EX_RunJobsInProcess` — when `true`, API process runs jobs in-process (all-in-one mode).
- `ASPNETCORE_URLS` — listen URLs (default `http://+:8080`).
- `EX_Html5Mode` — enables SPA routing fallback.
- `ConnectionStrings:Email` — SMTP connection (e.g., `smtp://localhost:1025`).
- `ConnectionStrings:Elasticsearch`, `ConnectionStrings:Redis` — infrastructure.

## Health Probes

- `/health` — ASP.NET health check endpoint.
- `/ready` — readiness probe.
- Aspire configures `WithHttpHealthCheck("/health")` for orchestration.

## Self-Contained Mode

The `exceptionless` image bundles Elasticsearch, API, Jobs, and frontend via supervisord.

Environment defaults in self-contained mode:

- `EX_ConnectionStrings__Storage=provider=folder;path=/app/storage`
- `EX_RunJobsInProcess=true`

### Requirement: Self-hosted upgrades require backups

Self-hosted Exceptionless upgrade guidance must require creating backups before upgrading.

#### Scenario: Self-hosted instance is upgraded

Given a self-hosted Exceptionless deployment
When the deployment is upgraded
Then operators must create backups before upgrade work begins.

**Notes:** The documented major upgrade path uses an intermediate Elasticsearch 7-compatible image to migrate data before switching to the current Elasticsearch 8-based image. The self-contained single-node Docker setup has no built-in backup mechanism; users are responsible for backups.

## CI/CD

- Standard PRs build: `api`, `job`, `app` images.
- Tag pushes build: all images including `exceptionless`.
- GitHub Actions workflow manages the build pipeline.

## Exposed Port

All runtime images expose port `8080`.

## Kubernetes and Helm

### Requirement: Kubernetes deployment assets are maintained

Exceptionless includes Kubernetes and Helm deployment assets under `k8s/`.

#### Scenario: Deployment behavior changes

Given a change affects deployment behavior, infrastructure topology, or environment configuration
When Kubernetes or Helm assets are affected
Then the corresponding files under `k8s/` must be reviewed and updated.

**Notes (implementation-derived):** The `k8s/` directory contains a Helm chart (`k8s/exceptionless/Chart.yaml`), values files, Elasticsearch manifests, migration job manifests, and separate dev/prod values. Kubernetes manifests include separate `args: [DataMigration]` and `args: [Migration]` job manifests. Dev example: single Elasticsearch node with 12Gi memory and 100Gi storage. Prod example: four Elasticsearch nodes with 18Gi memory and 600Gi storage each. These are maintained examples, not universal minimums.

## Elasticsearch Version

### Requirement: Elasticsearch version guidance follows maintained deployment assets

Exceptionless self-hosted deployments must use Elasticsearch 8 images. The current maintained image is the version configured in deployment assets; ES7 prebuilt all-in-one images have been dropped.

#### Scenario: Elasticsearch image version changes

Given the maintained Elasticsearch image version changes
When deployment manifests or Helm values are updated
Then the change must document migration, compatibility, and backup considerations.

**Notes:** The maintained Elasticsearch version is whatever is currently pinned in the deployment assets. Minimum is latest 8.x; the upper bound is limited by NEST client compatibility. Do not encode specific image tags in specs — they change.

## Aspire Orchestration (Dev)

- Elasticsearch, Redis, Azure Storage emulator, Mailpit launched as containers.
- `--services-only` flag starts infrastructure without application services.
- Persistent containers/volumes in non-services-only mode.
- Kibana and RedisInsight available as dev-time companions.

## Compatibility Boundaries

- Environment variable names (`EX_*`, `ConnectionStrings:*`) are deployment contracts.
- Docker image names and tags are consumed by users' deployment configs.
- Port 8080 is the standard exposed port.
- The `--no-build` publish stages require build output and NuGet cache from prior stages.
- `app-docker-entrypoint.sh` and `update-config*` scripts are part of the runtime contract for self-hosted deployments.

### Requirement: Production Elasticsearch topology guidance is example-based

Exceptionless production documentation recommends Kubernetes and provides Elasticsearch cluster examples, but the maintained Kubernetes manifests are examples rather than a formal minimum production topology.

#### Scenario: Elasticsearch topology changes

Given a change modifies Elasticsearch node counts, roles, shards, replicas, or connection-string guidance
When the deployment assets or documentation are updated
Then the change must document whether the update is an example, a recommendation, or a new minimum requirement.

### Requirement: Local and deployment resource guidance is example-based

Local Docker setup requires Docker and is not production guidance. Kubernetes resource values in maintained manifests are deployment examples, not universal local-development minimums.

#### Scenario: Resource guidance changes

Given CPU, memory, disk, replica, or storage values change in deployment assets
When the change is proposed
Then the change must state whether the values are examples, recommendations, or minimum requirements.

**Note on backup/restore:** No detailed Elasticsearch backup/restore runbook exists beyond the documented guidance to create backups before upgrading. The simple self-contained Docker setup has no built-in backup mechanism.
