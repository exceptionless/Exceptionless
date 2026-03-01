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

Test filtering note: the backend test project uses Microsoft Testing Platform, so targeted runs use test-app options after `--`, for example `dotnet test -- --filter-class Exceptionless.Tests.Controllers.EventControllerTests`.

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

Each time you complete a task or learn important information about the project, you must update the `AGENTS.md`, `README.md`, or relevant skill files. **Only update skills if they are owned by us** (verify via `skills-lock.json` which lists third-party skills). You are **forbidden** from updating skills, configurations, or instructions maintained by third parties/external libraries.

If you encounter recurring questions or patterns during planning, document them:

- Project-specific knowledge → `AGENTS.md` or relevant skill file
- Reusable domain patterns → Create/update appropriate skill in `.agents/skills/`

## Skills

Load from `.agents/skills/<name>/SKILL.md` when working in that domain:

| Domain        | Skills                                                                                                                                                    |
| ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend       | dotnet-conventions, backend-architecture, dotnet-cli, backend-testing, foundatio                                                                          |
| Frontend      | svelte-components, tanstack-form, tanstack-query, shadcn-svelte, typescript-conventions, frontend-architecture, storybook, accessibility, frontend-design |
| Testing       | frontend-testing, e2e-testing                                                                                                                             |
| Cross-cutting | security-principles, releasenotes                                                                                                                         |
| Billing       | stripe-best-practices, upgrade-stripe                                                                                                                     |
| Agents        | agent-browser, dogfood                                                                                                                                    |
| Meta          | skill-evolution                                                                                                                                           |

## Agents

Available in `.claude/agents/`. Use `@agent-name` to invoke:

- `engineer`: Use for implementing features, fixing bugs, or making code changes — plans, TDD, implements, verify loop, ships end-to-end
- `reviewer`: Use for reviewing code quality — adversarial 4-pass analysis (security → build → correctness → style). Read-only.
- `triage`: Use for analyzing issues, investigating bugs, or answering codebase questions — impact assessment, RCA, reproduction, implementation plans
- `pr-reviewer`: Use for end-to-end PR review — zero-trust security pre-screen, dependency audit, delegates to @reviewer, delivers verdict

### Orchestration Flow

```text
engineer → TDD → implement → verify (loop until clean)
         → @reviewer (loop until 0 blockers) → commit → push → PR
         → @copilot review → CI checks → resolve feedback → merge

triage → impact assessment → deep research → RCA → reproduce
       → implementation plan → post to GitHub → @engineer

pr-reviewer → security pre-screen (before build!) → dependency audit
            → build → @reviewer (4-pass) → verdict
```

## Constraints

- Use `npm ci` (not `npm install`)
- Never commit secrets — use environment variables
- NuGet feeds are in `NuGet.Config` — don't add sources
- Prefer additive documentation updates — don't replace strategic docs wholesale, extend them
