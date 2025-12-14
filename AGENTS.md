# Agent Guidelines for the Exceptionless Repository

You are an expert engineer working on Exceptionless, a production-grade real-time error monitoring platform handling billions of requests. Your changes must maintain backward compatibility, performance, and reliability. Approach each task methodically: research existing patterns, make surgical changes, and validate thoroughly.

**Craftsmanship Mindset**: Every line of code should be intentional, readable, and maintainable. Write code you'd be proud to have reviewed by senior engineers. Prefer simplicity over cleverness. When in doubt, favor explicitness and clarity.

## Quick Start

Run `Exceptionless.AppHost` from your IDE. Aspire automatically starts all required services (Elasticsearch, Redis) with proper ordering. The Aspire dashboard opens at the assigned localhost port, resources and logs can be accessed via the Aspire MCP.

## Scoped Guidance

Follow the AGENTS.md closest to the code you're changing:

- **Backend (.NET):** `src/AGENTS.md`
- **Web API:** `src/Exceptionless.Web/AGENTS.md`
- **Frontend (Svelte SPA):** `src/Exceptionless.Web/ClientApp/AGENTS.md`
- **Svelte Components:** `src/Exceptionless.Web/ClientApp/src/AGENTS.md`
- **E2E Tests (Playwright):** `src/Exceptionless.Web/ClientApp/e2e/AGENTS.md`
- **Backend Tests (C#):** `tests/AGENTS.md`

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
  Integration tests use `AppWebHostFactory` with Aspire (see `tests/AGENTS.md`).

- **Frontend:** See `src/Exceptionless.Web/ClientApp/AGENTS.md` for unit and E2E testing commands.

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

## Coding Standards

### Style & Formatting

- Follow `.editorconfig` rules strictly—4 spaces, file-scoped namespaces, usings outside namespaces, braces for all control blocks
- Run formatters before committing: `dotnet format` for C#, `npm run format` for frontend
- Match existing file style; minimize diffs
- No code comments unless necessary—code should be self-explanatory

### C# Conventions

- **Naming:** Private fields `_camelCase`. public members PascalCase
- **Types:** Use explicit types when obvious; `var` is acceptable elsewhere per `.editorconfig`
- **Nullable:** Honor nullable annotations and treat warnings as errors
- **Async:** Use `Async` suffix, pass `CancellationToken` through call chains, prefer `ValueTask<T>` for hot paths
- **Resources:** Always dispose properly with `using` statements or `IAsyncDisposable`

### TypeScript/Svelte Conventions

- Follow ESLint/Prettier config strictly
- Use kebab-case filenames, prefer named imports
- Avoid namespace imports except allowed barrels/shadcn
- Always await Promises; handle errors with try/catch
- Avoid `any`—use interfaces/types and type guards

### Single Responsibility

- Each class/component has one reason to change
- Methods do one thing well; extract when doing multiple things
- Keep files focused: one primary type per file
- Separate concerns: don't mix I/O, business logic, and presentation
- If a method needs a comment explaining what it does, it should probably be extracted

### Accessibility (WCAG 2.2 AA)

- Semantic landmarks and keyboard-first navigation
- Correct roles/labels and proper alt text
- Follow the frontend AGENTS for detailed patterns

## Making Changes

### Before Starting

1. **Gather context**: Read related files, search for similar implementations, understand the full scope
2. **Research patterns**: Find existing usages of the code you're modifying using grep/semantic search
3. **Understand completely**: Know the problem, side effects, and edge cases before coding
4. **Plan the approach**: Choose the simplest solution that satisfies all requirements
5. **Check dependencies**: Verify you understand how changes affect dependent code

### Pre-Implementation Analysis

Before writing any implementation code, think critically:

1. **What could go wrong?** Consider race conditions, null references, edge cases, resource exhaustion
2. **What are the failure modes?** Network failures, timeouts, out-of-memory, concurrent access
3. **What assumptions am I making?** Validate each assumption against the codebase
4. **Is this the root cause?** Don't fix symptoms—trace to the core problem
5. **Will this scale?** Consider performance under load, memory allocation patterns
6. **Is there existing code that does this?** Search before creating new utilities

### Test-First Development

**Always write or extend tests before implementing changes:**

1. **Find existing tests first**: Search for tests covering the code you're modifying
2. **Extend existing tests**: Add test cases to existing test classes/methods when possible for maintainability
3. **Write failing tests**: Create tests that demonstrate the bug or missing feature
4. **Implement the fix**: Write minimal code to make tests pass
5. **Refactor**: Clean up while keeping tests green
6. **Verify edge cases**: Add tests for boundary conditions and error paths

**Why extend existing tests?** Consolidates related test logic, reduces duplication, improves discoverability, maintains consistent test patterns.

### While Coding

- **Minimize diffs**: Change only what's necessary, preserve formatting and structure
- **Preserve behavior**: Don't break existing functionality or change semantics unintentionally
- **Build incrementally**: Run builds after each logical change to catch errors early
- **Test continuously**: Run tests frequently to verify correctness
- **Match style**: Follow the patterns in surrounding code exactly

### Validation

Before marking work complete, verify:

1. **Builds successfully**: Both backend and frontend compile without errors
2. **All tests pass**: No test failures in affected areas
3. **No new warnings**: Check build output for new compiler/linter warnings
4. **API compatibility**: Public API changes are intentional and backward-compatible when possible
5. **Breaking changes flagged**: Clearly identify any breaking changes for review

## Error Handling

- **Validate inputs**: Check for null, empty strings, invalid ranges at method entry
- **Fail fast**: Throw exceptions immediately for invalid arguments (don't propagate bad data)
- **Meaningful messages**: Include parameter names and expected values in exception messages
- **Don't swallow exceptions**: Log and rethrow, or let propagate unless you can handle properly
- **Use guard clauses**: Early returns for invalid conditions, keep happy path unindented

## Security

- **Validate all inputs**: Use guard clauses, check bounds, validate formats before processing
- **Sanitize external data**: Never trust data from queues, caches, user input, or external sources
- **No sensitive data in logs**: Never log passwords, tokens, keys, or PII
- **Use secure defaults**: Default to encrypted connections, secure protocols, restricted permissions
- **Follow OWASP guidelines**: Review [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- **No deprecated APIs**: Avoid obsolete cryptography, serialization, or framework features

## Dependencies

- **NuGet:** Feeds are defined in `NuGet.Config` (Feedz + local `build/packages`); do not add new sources unless requested
- **SDK:** Shared settings live in `src/Directory.Build.props`; keep target frameworks/versioning consistent
- **npm:** Keep `package-lock.json` in sync and use `npm ci` for reproducible installs
- **Secrets:** Do not commit secrets; use environment variables or secret managers

## Testing Philosophy

Tests are not just validation—they're **executable documentation** and **design tools**. Well-tested code is:

- **Trustworthy**: Confidence to refactor and extend
- **Documented**: Tests show how the API should be used
- **Resilient**: Edge cases are covered before they become production bugs

### Test Principles (FIRST)

- **Fast**: Tests execute quickly
- **Isolated**: No dependencies on external services or execution order
- **Repeatable**: Consistent results every run
- **Self-checking**: Tests validate their own outcomes
- **Timely**: Write tests alongside code

### Test Layers

Prefer the most targeted test layer while covering critical paths end-to-end:

1. **Unit tests**: Fast, isolated, test single units of logic
2. **Integration tests**: Test component interactions with real dependencies
3. **E2E tests**: Test complete user workflows through the UI

## Resilience & Reliability

- **Expect failures**: Network calls fail, resources exhaust, concurrent access races
- **Timeouts everywhere**: Never wait indefinitely; use cancellation tokens
- **Retry with backoff**: Use exponential backoff with jitter for transient failures
- **Graceful degradation**: Return cached data, default values, or partial results when appropriate
- **Idempotency**: Design operations to be safely retryable
- **Resource limits**: Bound queues, caches, and buffers to prevent memory exhaustion

## Resources

- Use context7 when you need code generation, setup/config steps, or library/API docs
- Check existing patterns in the codebase before introducing new ones
