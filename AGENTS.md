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

## Continuous Improvement

Each time you complete a task or learn important information about the project, you must update the `AGENTS.md`, `README.md`, or relevant skill files. **Only update skills if they are owned by us** (verify via `.github/update-skills.ps1` which lists third-party skills). You are **forbidden** from updating skills, configurations, or instructions maintained by third parties/external libraries.

If you encounter recurring questions or patterns during planning, document them:

- Project-specific knowledge → `AGENTS.md` or relevant skill file
- Reusable domain patterns → Create/update appropriate skill in `.github/skills/`

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

## Serialization Architecture

The project uses **System.Text.Json (STJ)** exclusively. NEST still brings in Newtonsoft.Json transitively, but all application-level serialization uses STJ:

| Component      | Serializer                        | Notes                                       |
| -------------- | --------------------------------- | -------------------------------------------- |
| Elasticsearch  | `ElasticSystemTextJsonSerializer` | Custom `IElasticsearchSerializer` using STJ  |
| Event Upgrader | `System.Text.Json.Nodes`          | JsonObject/JsonArray for mutable DOM         |
| Data Storage   | `SystemTextJsonSerializer`        | Via Foundatio's STJ support                  |
| API            | STJ (built-in)                    | ASP.NET Core default with custom options     |

**Key files:**

- `ElasticSystemTextJsonSerializer.cs` - Custom `IElasticsearchSerializer` for NEST
- `JsonNodeExtensions.cs` - STJ equivalents of JObject helpers
- `ObjectToInferredTypesConverter.cs` - Handles JObject/JToken from NEST during STJ serialization
- `V*_EventUpgrade.cs` - Event version upgraders using JsonObject

**Security:**

- Safe JSON encoding used everywhere (escapes `<`, `>`, `&`, `'` for XSS protection)
- No `UnsafeRelaxedJsonEscaping` in the codebase
