# Copilot Instructions

This project features an **ASP.NET Core** backend (REST API) and a **Svelte 5 TypeScript** frontend (SPA).
All contributions must respect existing formatting and conventions specified in the `.editorconfig` file.
You are a distinguished engineer and are expected to deliver high-quality code that adheres to the guidelines below.

---

## 1. General Coding Guidelines

- **Code Style & Minimal Diffs:**
  - Match the file's existing style; use `.editorconfig` when unsure.
  - Preserve extra spaces, comments, and minimize diffs.
  - Always ask before creating new files, directories, or changing existing structures.
  - Always look at existing usages before refactoring or changing code to prevent new code from breaking existing code.

- **Modern Code Practices:**
  - Write complete, runnable code—no placeholders or TODOs.
  - Use modern language features, clear naming conventions, and defensive coding when necessary.
  - Follow SOLID, DRY, and clean code principles. Remove unused code.

- **Behavior Management:**
  - Flag any user-visible changes for review.
  - Deliver exactly what’s requested—avoid adding unnecessary features unless explicitly instructed.

---

## 2. Frontend Guidelines (Svelte 5 / TypeScript SPA)

Located in the `src/Exceptionless.Web/ClientApp` directory.

- **Framework & Best Practices:**
  - Use Svelte 5 in SPA mode with TypeScript and Tailwind CSS.
  - Follow modern ES6 best practices and the ESLint recommended configuration ([standardjs](https://standardjs.com)).
  - Code can be formatted and linted with `npm run format` and checked for errors with `npm run check`.

- **Architecture & Components:**
  - Follow the Composite Component Pattern.
  - Organize code into vertical slices (e.g., features aligned with API controllers) and maintain shared components in a central folder.
  - Use **kebab-case** for filenames and directories (e.g., `components/event-overview.svelte`).
  - Reexport generated code `src/Exceptionless.Web/ClientApp/src/lib/generated` from the respective feature models folder.
  - **Do NOT** use any server-side Svelte features.

- **UI, Accessibility & Testing:**
  - Ensure excellent keyboard navigation for all interactions.
  - Build forms with shadcn-svelte forms & superforms, and validate with class-validator.
    - Good examples are the manage account and login pages.
  - Use formatters `src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/formatters` for displaying built-in types (date, number, boolean, etc.).
  - Ensure semantic HTML, mobile-first design, and WCAG 2.2 Level AA compliance.
  - Use shadcn-svelte components (based on [bits-ui](https://bits-ui.com/docs/llms.txt)).
    - Look for new components in the shadcn-svelte documentation

- **API Calls:**
  - Use TanStack Query for all API calls centralized in an `api.svelte.ts` file.
  - Leverage `@exceptionless/fetchclient` for network operations.

- **Testing Tools:**
  - Unit Testing: Vitest
  - Component Testing: Testing Library
  - E2E Testing: Playwright

- **Reference documentation:**
  - Always use Svelte 5 features: [https://svelte.dev/llms-full.txt](https://svelte.dev/llms-full.txt)
    - on:click -> onclick

---

## 3. Backend Guidelines (ASP.NET Core / C#)

- **Framework & Best Practices:**
  - Use the latest ASP.NET Core with C# and enable Nullable Reference Types.
  - Code can be formatted with `dotnet format` and checked for errors with `dotnet build`.

- **Conventions & Best Practices:**
- Adhere to the `.editorconfig` file and Microsoft's [coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- Follow Microsoft's [unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices).

- **Architectural Considerations:**
  - Design services with awareness of distributed computing challenges.

---

## 4. Security Guidelines

- **Best Practices:**
  - Sanitize all user inputs and rigorously validate data.
  - Follow OWASP guidelines and implement a robust Content Security Policy.
  - Adopt Shift-Left security practices to identify vulnerabilities early.

---

## 5. Developer Planning & Reflection

- **Pre-Coding Reflection:**
  1. Identify the problem or feature you’re solving.
  2. Consider three possible approaches.
  3. Choose the simplest approach that satisfies all requirements.
  4. Clarify:
     - Can the solution be modularized into smaller functions?
     - Are there unnecessary abstractions?
     - Will the implementation be clear to a junior developer?

- **Post-Coding Reflection:**
  1. Review for refactor opportunities—can clarity or maintainability be improved?
  2. Identify potential edge cases or areas prone to bugs.
  3. Verify robust error handling and validation mechanisms.

---

## 6. Code Reviews

- **Focus Areas:**
  - Ensure adherence to complexity, consistency, and clean code standards.
  - Validate robust error handling and defensive coding practices.
  - Check for duplication and maintainable solutions.

---

## 7. Debugging Guidelines

1. **Reproduce** the issue with minimal steps and code.
2. **Understand** the underlying problem thoroughly.
3. **Form Hypotheses** about the cause.
4. **Test & Verify** potential solutions.
5. **Document** fixes and adjustments clearly for future reference.

---

## 8. Project Structure

```plaintext
project-root/
├── build                                           # Build files
├── docker                                          # Docker files
├── k8s                                             # Kubernetes files
├── samples
├── src
│   ├── Exceptionless.AppHost                       # Aspire
│   ├── Exceptionless.Core                          # Domain
│   ├── Exceptionless.EmailTemplates                # Email Templates
│   ├── Exceptionless.Insulation                    # Concrete Implementations
│   ├── Exceptionless.Job                           # ASP.NET Core Jobs
│   ├── Exceptionless.Web                           # ASP.NET Core Web Application
│   │   ├── ClientApp                               # Frontend SvelteKit Spa Application
│   │   │   ├── api-templates                       # API templates for generated code using OpenApi
│   │   │   ├── e2e
│   │   │   ├── src                                 # JavaScript SvelteKit application
│   │   │   │   ├── lib
│   │   │   │   │   ├── assets                      # Static assets
│   │   │   │   │   ├── features                    # Vertical Sliced Features, each folder corresponds to an api controller
│   │   │   │   │   │   ├── events                  # Event features (related to Events Controller)
│   │   │   │   │   │   │   ├── components
│   │   │   │   │   │   │   └── models
│   │   │   │   │   │   ├── organizations
│   │   │   │   │   │   ├── projects
│   │   │   │   │   │   │   └── components
│   │   │   │   │   │   ├── shared                  # Shared components used by all other features
│   │   │   │   │   │   │   ├── api
│   │   │   │   │   │   │   ├── components
│   │   │   │   │   │   │   └── models
│   │   │   │   │   │   ├── stacks
│   │   │   │   │   │   │   └── components
│   │   │   │   │   ├── generated                   # Generated code
│   │   │   │   │   ├── hooks                       # Client hooks
│   │   │   │   │   └── utils                       # Utility functions
│   │   │   │   └── routes                          # Application routes
│   │   │   │       ├── (app)
│   │   │   │       │   ├── account
│   │   │   │       │   └── stream
│   │   │   │       ├── (auth)
│   │   │   │       │   ├── login
│   │   │   │       │   └── logout
│   │   │   │       └── status
│   │   │   └── static                              # Static assets
│   │   ├── ClientApp.angular                       # Legacy Angular Client Application (Ignore)
│   │   ├── Controllers                             # ASP.NET Core Web API Controllers
│   └── tests                                       # ASP.NET Core Unit and Integration Tests
```

---

### Additional Considerations

- **Expanding the Guidelines:**
  As the project evolves, consider including sample code snippets, decision flowcharts, or ASCII diagrams to clarify more complex guidelines.

- **Continuous Improvement:**
  Regularly review and update these guidelines to stay aligned with evolving best practices and emerging technologies.

- **Thoughtful Contribution:**
  Always strive to deliver code that not only functions well but also advances the overall maintainability and quality of the project.

Let's keep pushing for clarity, usability, and excellence—both in code and user experience.
