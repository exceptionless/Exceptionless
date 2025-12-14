# Agent Guidelines for the Exceptionless Repository

You are contributing to **Exceptionless**, a real-time error monitoring platform. Backend services run on .NET 10; the SPA lives in `src/Exceptionless.Web/ClientApp` (Svelte 5 + TypeScript).

## Quick Start

Run `Exceptionless.AppHost` from your IDE. Aspire automatically starts all required services (Elasticsearch, Redis, Mailpit) with proper ordering. The Aspire dashboard opens at the assigned localhost port.

**Alternative (infrastructure only):**

```bash
cd docker && docker compose up -d elasticsearch redis mail
```

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

- **.NET SDK 10.0** — pinned in `global.json`
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
src/
├── Exceptionless.AppHost      # Aspire orchestrator (start here for full stack)
├── Exceptionless.Core         # Domain logic and services
├── Exceptionless.Insulation   # Concrete implementations (Elasticsearch, Redis, etc.)
├── Exceptionless.Web          # ASP.NET Core API + SPA host
│   └── ClientApp              # Svelte 5 SPA (primary client)
│   └── ClientApp.angular      # Angular SPA (legacy client)
└── Exceptionless.Job          # Background workers

tests/                         # C# integration/unit tests + HTTP samples
docker/                        # Compose files for local services
build/                         # Scripts and templates
```

## Coding Standards

- Follow `.editorconfig` (4 spaces, file-scoped namespaces, usings outside namespaces, braces for all control blocks).
- **C#:** Use explicit types when obvious; `var` is acceptable elsewhere per `.editorconfig`. Private fields `_camelCase`, static fields `s_camelCase`, public members PascalCase. Honor nullable annotations; keep files trimmed with a final newline.
- **TypeScript/Svelte:** Follow ESLint/Prettier config and the frontend AGENTS. Use kebab-case filenames, prefer named imports, avoid namespace imports except allowed barrels/shadcn. Always await async work and keep single-line control statements wrapped in braces.
- **Accessibility:** Meet WCAG 2.2 AA; ensure keyboard navigation, semantic landmarks, correct roles/labels, and proper alt text. Follow the frontend AGENTS for detailed patterns.
- Avoid unnecessary abstractions; match existing patterns before introducing new ones.
- **Comments:** Keep minimal; prefer self-explanatory code and existing patterns.

## Making Changes

- Keep diffs surgical and match surrounding patterns. Ask before adding new files or changing structure.
- Preserve existing behavior unless explicitly changing it; assume uncommitted code is correct.
- Run the relevant build/test commands (backend and/or frontend) before handing off changes.
- Flag user-visible changes. Prefer small, logical commits.
- Use context7 when you need code generation, setup/config steps, or library/API docs.

## Dependencies & Security

- **NuGet:** Feeds are defined in `NuGet.Config` (Feedz + local `build/packages`); do not add new sources unless requested.
- **SDK:** Shared settings live in `src/Directory.Build.props`; keep target frameworks/versioning consistent.
- **npm:** Keep `package-lock.json` in sync and use `npm ci` for reproducible installs.
- **Secrets:** Do not commit secrets. Follow secure coding practices and sanitize inputs.

## Testing Philosophy

- Write tests for new functionality and fix failures related to your changes; do not remove existing tests.
- Keep tests fast, isolated, and self-checking. Use Arrange/Act/Assert and descriptive names.
- Prefer the most targeted test layer (unit > integration > e2e) while covering critical paths end-to-end where it matters.
