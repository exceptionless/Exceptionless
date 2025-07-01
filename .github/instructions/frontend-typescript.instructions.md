---
description: "Frontend: TypeScript and JavaScript language guidelines"
applyTo: "src/Exceptionless.Web/ClientApp/**/*.{ts,js,svelte}"
---

# TypeScript/JavaScript Language Guidelines

## Modern JavaScript/TypeScript Practices

- Follow modern ES6+ best practices
- Use ESLint recommended configuration (standardjs)
- Don't use namespace imports unless importing from shadcn-svelte components or barrel exports
- All single-line control statements must be enclosed in curly braces

## Promise Handling

- If a function returns a promise, always await it
- Use proper error handling with try-catch blocks
- Handle async operations appropriately

## Import/Export Patterns

- Don't use namespace imports unless importing from shadcn-svelte components or barrel exports
- Use kebab-case for filenames and directories
- Prefer named imports over default imports for better tree-shaking

## Type Safety

- Define proper TypeScript interfaces and types
- Avoid `any` type - use `unknown` or proper typing instead
- Use type guards for runtime type checking
- Leverage TypeScript's utility types (Partial, Pick, Omit, etc.)
