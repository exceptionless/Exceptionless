# Agent Guidelines for the Exceptionless Repository

You are an expert engineer working on Exceptionless, a production-grade real-time error monitoring platform handling billions of requests. Your changes must maintain backward compatibility, performance, and reliability. Approach each task methodically: research existing patterns, make surgical changes, and validate thoroughly.

**Craftsmanship Mindset**: Every line of code should be intentional, readable, and maintainable. Write code you'd be proud to have reviewed by senior engineers. Prefer simplicity over cleverness. When in doubt, favor explicitness and clarity.

## Quick Start

Run `Exceptionless.AppHost` from your IDE. Aspire automatically starts all required services (Elasticsearch, Redis) with proper ordering. The Aspire dashboard opens at the assigned localhost port, resources and logs can be accessed via the Aspire MCP.

## Skills Reference

Load skills on-demand for detailed patterns and guidelines. When working in a domain, read the corresponding SKILL.md file from `.github/skills/<skill-name>/`.

### Backend Skills

- **dotnet-conventions** — C# naming, formatting, async patterns, nullable
- **backend-architecture** — Project layering, repositories, controllers, Aspire
- **dotnet-cli** — Build, test, and format commands
- **backend-testing** — xUnit patterns, AppWebHostFactory, integration tests
- **foundatio** — Caching, queues, message bus, jobs, resilience

### Frontend Skills

- **svelte-components** — Svelte 5, runes, $state, $derived, $effect, snippets
- **tanstack-form** — TanStack Form, Zod validation, error handling
- **tanstack-query** — Data fetching, caching, mutations, WebSocket invalidation
- **shadcn-svelte** — UI components, trigger patterns, dialogs
- **typescript-conventions** — Naming, imports, type safety
- **frontend-architecture** — Route groups, feature slices, lib structure
- **storybook** — Component stories, defineMeta, visual testing

### Testing Skills

- **frontend-testing** — Vitest, Testing Library, component tests
- **e2e-testing** — Playwright, Page Object Model, accessibility audits

### Cross-Cutting Skills

- **security-principles** — Secrets, encryption, secure defaults, OWASP
- **accessibility** — WCAG 2.2 AA, ARIA, keyboard navigation

## Build System

### Prerequisites

- **.NET SDK 10.0**
- **Node 24+**
- **npm** — use the committed `package-lock.json`

### Backend Build

```bash
dotnet restore
dotnet build
```

### Frontend Build

```bash
cd src/Exceptionless.Web/ClientApp
npm ci
npm run build
```

## Testing

- **Backend:** `dotnet test`
- **Frontend Unit:** `npm run test:unit`
- **Frontend E2E:** `npm run test:e2e`

## Project Structure

```text
src
├── Exceptionless.AppHost      # Aspire orchestrator (start here for full stack)
├── Exceptionless.Core         # Domain logic and services
├── Exceptionless.Insulation   # Concrete implementations (Elasticsearch, Redis, etc.)
├── Exceptionless.Web          # ASP.NET Core API + SPA host
│   └── ClientApp              # Svelte 5 SPA (primary client)
│   └── ClientApp.angular      # Angular SPA (legacy client)
└── Exceptionless.Job          # Background workers
tests                          # C# integration/unit tests + HTTP samples
docker                         # Compose files for local services
build                          # Scripts and templates
```

## Making Changes

### Before Starting

1. **Gather context**: Read related files, search for similar implementations
2. **Research patterns**: Find existing usages using grep/semantic search
3. **Understand completely**: Know the problem, side effects, and edge cases
4. **Plan the approach**: Choose the simplest solution that satisfies requirements
5. **Check dependencies**: Verify how changes affect dependent code

### Pre-Implementation Analysis

Before writing code, think critically:

1. **What could go wrong?** Race conditions, null references, edge cases
2. **What are the failure modes?** Network failures, timeouts, concurrent access
3. **What assumptions am I making?** Validate against the codebase
4. **Is this the root cause?** Don't fix symptoms—trace to the core problem
5. **Is there existing code that does this?** Search before creating new utilities

### Single Responsibility Principle

- Each class/component has **one reason to change**
- Methods do one thing well; extract when doing multiple things
- Keep files focused: one primary type per file
- Separate concerns: don't mix I/O, business logic, and presentation
- If a method needs a comment explaining what it does, it should probably be extracted

### Test-First Development

**Always write or extend tests before implementing changes:**

1. **Find existing tests first**: Search for tests covering the code you're modifying
2. **Extend existing tests**: Add test cases to existing test classes when possible
3. **Write failing tests**: Demonstrate the bug or missing feature
4. **Implement the fix**: Write minimal code to make tests pass
5. **Refactor**: Clean up while keeping tests green

### Validation

Before marking work complete:

1. **Builds successfully**: Both backend and frontend compile without errors
2. **All tests pass**: No test failures in affected areas
3. **No new warnings**: Check build output for compiler/linter warnings
4. **API compatibility**: Breaking changes are intentional and flagged

## Testing Philosophy

Tests are **executable documentation** and **design tools**:

- **Fast & Isolated**: No external dependencies or execution order
- **Repeatable & Self-checking**: Consistent results, validate outcomes
- **Timely**: Write tests alongside code

### Test Layers

1. **Unit tests**: Fast, isolated, test single units of logic
2. **Integration tests**: Component interactions with real dependencies
3. **E2E tests**: Complete user workflows through the UI

## Dependencies

- **NuGet:** Feeds in `NuGet.Config`; do not add sources unless requested
- **SDK:** Shared settings in `src/Directory.Build.props`
- **npm:** Keep `package-lock.json` in sync; use `npm ci`
- **Secrets:** Never commit secrets; use environment variables

## Resources

- Use context7 MCP for library API docs and code generation
- Load skills for detailed patterns (see Skills Reference above)
- Check existing patterns in the codebase before introducing new ones
