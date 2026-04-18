# Exceptionless

Real-time error monitoring platform handling billions of requests (ASP.NET Core 10 + Svelte 5). Act as a distinguished engineer focusing on readability, performance while maintaining backwards compatibility.

## Start Here

- Run `Exceptionless.AppHost` from your IDE, or `dotnet run --project src/Exceptionless.AppHost` from the repo root.
- Do not add a separate `aspire start -- --services-only` step for local runs or backend tests. The AppHost starts required services, and integration tests bootstrap their own infrastructure.

## Common Commands

| Task | Command |
| --- | --- |
| Backend build | `dotnet build` |
| Backend test | `dotnet test --project tests/Exceptionless.Tests/Exceptionless.Tests.csproj` |
| Frontend build | `cd src/Exceptionless.Web/ClientApp && npm ci && npm run build` |
| Frontend unit tests | `cd src/Exceptionless.Web/ClientApp && npm run test:unit` |

## Repo-Specific Notes

- Backend test filtering uses Microsoft Testing Platform test-app options after `--`, for example `dotnet test --project tests/Exceptionless.Tests/Exceptionless.Tests.csproj -- --filter-class Exceptionless.Tests.Controllers.EventControllerTests`.
- Elasticsearch-backed repository or job tests should derive from `IntegrationTestsBase`, not `TestWithServices`.
- Standard pull requests build `api`, `job`, and `app` images. The all-in-one `exceptionless` image is only built for tags.
- If you touch Docker publish stages that use `dotnet publish --no-build`, make sure the stage still has the build output and NuGet package cache available.

## Project Structure

```text
src/
├── Exceptionless.AppHost      # Aspire orchestrator (start here)
├── Exceptionless.Core         # Domain logic
├── Exceptionless.Insulation   # Infrastructure (Elasticsearch, Redis, Azure)
├── Exceptionless.Web          # API + Svelte SPA (ClientApp/)
└── Exceptionless.Job          # Background workers
tests/                         # C# tests + HTTP samples
```

## Constraints

- Use `npm ci` (not `npm install`)
- Never commit secrets — use environment variables
- NuGet feeds are in `NuGet.Config` — don't add sources
- Prefer additive documentation updates — don't replace strategic docs wholesale, extend them
