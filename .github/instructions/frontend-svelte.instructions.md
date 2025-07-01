---
description: "Frontend: Svelte specific guidelines"
applyTo: "src/Exceptionless.Web/ClientApp/**/*.svelte"
---

# Svelte Component Guidelines

## Component Structure

- Use Svelte 5 syntax and features consistently
- Prefer `$state` and `$derived` over `$effect` when possible
- Always use `onclick` instead of `on:click`
- Use `import { page } from '$app/state'` instead of `'$app/stores'`
- Use snippets `{#snippet ...}` and `{@render ...}` instead of `<slot>` for content projection.

## Event Handling

- All single-line control statements must be enclosed in curly braces
- Use proper event handling patterns with Svelte 5 syntax

## Component Organization

- Follow kebab-case naming for component files
- Use the Composite Component Pattern
- Organize components within vertical slices aligned with API controllers

## Accessibility

- Ensure excellent keyboard navigation for all interactions
- Use semantic HTML elements
- Maintain WCAG 2.2 Level AA compliance
- Implement mobile-first design principles

## Reference Documentation

- Always use Svelte 5 features: [https://svelte.dev/llms-full.txt](https://svelte.dev/llms-full.txt)
  - on:click -> onclick
  - import { page } from '$app/stores'; -> import { page } from '$app/state'
  - <slot> -> {#snippet ...}
  - beforeUpdate/afterUpdate -> $effect.pre
