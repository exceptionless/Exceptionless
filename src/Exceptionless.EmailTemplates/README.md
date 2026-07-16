# Exceptionless Email Templates

Svelte 5 email templates using [better-svelte-email](https://github.com/Konixy/better-svelte-email) with Tailwind CSS.

## Quick Start

```bash
npm ci
npm run build
npm run storybook
```

This compiles all Svelte email templates to static HTML with inlined CSS and writes them to `../Exceptionless.Core/Mail/Templates/`.

## Architecture

```
src/
├── components/          # Shared layout components
│   ├── EmailLayout.svelte    # Header + body wrapper
│   ├── ActionsFooter.svelte  # "Other Actions" bullet list
│   └── SocialFooter.svelte   # Social links footer
├── templates/           # One .svelte file per email template
│   ├── user-password-reset.svelte
│   ├── user-email-verify.svelte
│   ├── event-notice.svelte
│   ├── project-daily-summary.svelte
│   ├── organization-added.svelte
│   ├── organization-invited.svelte
│   ├── organization-notice.svelte
│   └── organization-payment-failed.svelte
└── build-emails.ts      # Build script (render + clean + validate + write)
```

## How It Works

1. **Build-time**: Vite compiles Svelte components in SSR mode
2. **Render**: `@better-svelte-email/server` renders each template to HTML with inlined Tailwind CSS
3. **Clean**: The build script strips Svelte artifacts (SSR comments) and validates Handlebars token balance
4. **Output**: Static HTML files with `{{HandlebarsTokens}}` that the .NET runtime fills at send-time

The generated file inventory is tracked in `generated-templates.json`. The build removes outputs that were previously
managed by this project when their source template is removed.

Storybook includes representative variants for conditional template branches. It uses escaped Handlebars rendering for
preview data; the C# mailer tests remain authoritative for production HandlebarsDotNet output and hostile input handling.

## Handlebars Tokens

Templates use literal Handlebars syntax that passes through Svelte compilation unchanged:

- Simple tokens: `{'{{UserFullName}}'}`
- Block helpers: `{@html '{{#if Condition}}'}...{@html '{{/if}}'}`
- Each loops: `{@html '{{#each Items}}'}...{@html '{{/each}}'}`

At runtime, `HandlebarsDotNet` in `Mailer.cs` compiles and renders these with real data.

## Adding a New Template

1. Create `src/templates/my-template.svelte`
2. Import and use the `EmailLayout` component
3. Register the component in `src/build-emails.ts`
4. Run `npm run build`
5. Add the output HTML as an EmbeddedResource in `Exceptionless.Core.csproj`
6. Add rendering logic in `Mailer.cs`
