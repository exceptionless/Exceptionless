# Exceptionless

Real-time error monitoring platform handling billions of requests (ASP.NET Core 10 + Svelte 5). Act as a distinguished engineer focusing on readability, performance while maintaining backwards compatibility.

## Quick Start

Run `Exceptionless.AppHost` from your IDE. Aspire starts all services (Elasticsearch, Redis) automatically.

## Build & Test

| Task           | Command                                                         |
| -------------- | --------------------------------------------------------------- |
| Backend build  | `dotnet build`                                                  |
| Backend test   | `dotnet test`                                                   |
| Frontend build | `cd src/Exceptionless.Web/ClientApp && npm ci && npm run build` |
| Frontend test  | `npm run test:unit`                                             |
| E2E test       | `npm run test:e2e`                                              |

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

## Skills

Load from `.github/skills/<name>/SKILL.md` when working in that domain:

| Domain        | Skills                                                                                                                                   |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Backend       | dotnet-conventions, backend-architecture, dotnet-cli, backend-testing, foundatio                                                         |
| Frontend      | svelte-components, tanstack-form, tanstack-query, shadcn-svelte, typescript-conventions, frontend-architecture, storybook, accessibility |
| Testing       | frontend-testing, e2e-testing                                                                                                            |
| Cross-cutting | security-principles                                                                                                                      |

## Constraints

- Use `npm ci` (not `npm install`)
- Never commit secrets — use environment variables
- NuGet feeds are in `NuGet.Config` — don't add sources
