# Copilot Instructions

This project features an **ASP.NET Core** backend (REST API) and a **Svelte 5 TypeScript** frontend (SPA). All contributions should respect existing formatting and conventions specified in the project’s `.editorconfig`.

---

## 1. General Coding Guidelines

- **Maintain File Style & Minimal Diffs:**
  - Match the code style of the file. When unsure, use the style defined in the `.editorconfig`.
  - Keep diffs as small as possible. Preserve existing formatting, including extra spaces and comments.

- **Complete, Clear, and Modern Code:**
  - Write complete code for every step—no placeholders or TODOs.
  - Prioritize readability by using modern language features, clear naming, and clean code practices.
  - It's acceptable to employ defensive coding practices when needed to handle unexpected scenarios gracefully.

- **Design Principles:**
  - Follow SOLID, DRY, and overall clean code principles.
  - Remove unused code; simplicity equals maintainability.

- **Behavioral Changes:**
  - Flag any user-visible changes and review them carefully.

---

## 2. Frontend Guidelines (Svelte 5 / TypeScript SPA)

- **Framework & Best Practices:**
  - Built with Svelte 5 in SPA mode using TypeScript and Tailwind CSS.
  - Adhere to modern ES6 best practices and the ESLint recommended configuration ([standardjs](https://standardjs.com)).

- **Architecture & Components:**
  - Utilize the Composite Component Pattern (similar to shadcn-svelte).
  - Organize code using vertical slices for features with a shared folder for common components.
  - **Avoid** using any server-side Svelte features.

- **UI, Accessibility, and Navigation:**
  - Ensure excellent keyboard navigation for all front-end interactions.
  - Leverage shadcn-svelte components, which are built on [bits-ui](https://bits-ui.com/docs/llms.txt).
  - Build forms using shadcn-svelte forms & superforms, and validate with class-validator.
  - For icons use lucide-svelte.
  - Follow mobile-first responsive design, semantic HTML, and secure WCAG 2.2 Level AA compliance.

- **Testing:**
  - Unit Testing: Vitest
  - Component Testing: Testing Library
  - E2E Testing: Playwright

- **Reference:**
  - Svelte 5 SPA guidelines: [https://svelte.dev/llms-full.txt](https://svelte.dev/llms-full.txt)

---

## 3. Backend Guidelines (ASP.NET Core / C#)

- **Framework & Language Features:**
  - Developed with the latest ASP.NET Core and C#.
  - Enable Nullable Reference Types.

- **Coding Conventions & Best Practices:**
  - Follow guidelines from the `.editorconfig` and Microsoft's [coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
  - Adhere to Microsoft's best practices for ASP.NET Core development and [unit testing with xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices).

- **Architecture Considerations:**
  - Factor in the inherent challenges of distributed computing when designing features.

---

## 4. Performance

- **Optimizations:**
  - Lazy load components when possible.
  - Optimize images and assets.
  - Employ effective caching strategies.

---

## 5. Security

- **Data Protection & Validation:**
  - Sanitize all user inputs and validate data rigorously.
  - Follow OWASP guidelines and implement a robust Content Security Policy.
  - Embrace Shift-Left security practices.

---

## 6. Code Reviews

- **Focus Areas:**
  - Review for complexity, consistency, duplication, and adherence to best practices.
  - Verify robust error handling and the effectiveness of defensive coding when applied.

---

## 7. Debugging Guidelines

1. **Reproduce** the issue using minimal steps and code.
2. **Understand** the core problem.
3. **Hypothesize** the root cause.
4. **Test & Verify** the solution.
5. **Document** the fix and any adjustments made.

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
