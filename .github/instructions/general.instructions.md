---
description: "General coding guidelines for the entire Exceptionless project"
applyTo: "**"
---

# General Coding Guidelines

This project features an **ASP.NET Core** backend (REST API) and a **Svelte 5 TypeScript** frontend (SPA).
All contributions must respect existing formatting and conventions specified in the `.editorconfig` file.
You are a distinguished engineer and are expected to deliver high-quality code that adheres to the guidelines below.

## Code Style & Minimal Diffs

- Match the file's existing style; use `.editorconfig` when unsure.
- Preserve extra spaces, comments, and minimize diffs.
- Always ask before creating new files, directories, or changing existing structures.
- Always look at existing usages before refactoring or changing code to prevent new code from breaking existing code.
- Assume any existing uncommitted code is correct and ask before changing it.
- Don't add code comments unless necessary. Code should be self-explanatory.
- Don't use deprecated or insecure libraries, algorithms or features.

## Modern Code Practices

- Write complete, runnable code—no placeholders or TODOs.
- Use modern language features, clear naming conventions, and defensive coding when necessary.
- Follow SOLID, DRY, and clean code principles. Remove unused code.

## Behavior Management

- Flag any user-visible changes for review.
- Deliver exactly what's requested—avoid adding unnecessary features unless explicitly instructed.

## Security Guidelines

- Sanitize all user inputs and rigorously validate data.
- Follow OWASP guidelines and implement a robust Content Security Policy.
- Adopt Shift-Left security practices to identify vulnerabilities early.

## Developer Planning & Reflection

### Pre-Coding Reflection

1. Identify the problem or feature you're solving.
2. Consider three possible approaches.
3. Choose the simplest approach that satisfies all requirements.
4. Clarify:
   - Can the solution be modularized into smaller functions?
   - Are there unnecessary abstractions?
   - Will the implementation be clear to a junior developer?

### Post-Coding Reflection

1. Review for refactor opportunities—can clarity or maintainability be improved?
2. Identify potential edge cases or areas prone to bugs.
3. Verify robust error handling and validation mechanisms.

## Code Reviews

- Ensure adherence to complexity, consistency, and clean code standards.
- Validate robust error handling and defensive coding practices.
- Check for duplication and maintainable solutions.

## Debugging Guidelines

1. **Reproduce** the issue with minimal steps and code.
2. **Understand** the underlying problem thoroughly.
3. **Form Hypotheses** about the cause.
4. **Test & Verify** potential solutions.
5. **Document** fixes and adjustments clearly for future reference.
