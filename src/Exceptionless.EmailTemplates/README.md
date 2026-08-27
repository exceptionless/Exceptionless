# Exceptionless Email Templates

Svelte 5 sources in `src/templates` generate the Handlebars HTML consumed by `Exceptionless.Core`.

## Commands

```bash
npm ci
npm run build
npm run lint
npm run check
```

Use `npm run dev` to rebuild on source changes. Generated templates are written to
`../Exceptionless.Core/Mail/Templates` and must be committed with their Svelte sources.

## Template contract

- Every `src/templates/*.svelte` file must have one registry entry and one generated `.html` file with the same name.
- Generated HTML must contain a doctype, a `{{Subject}}` token, and valid Handlebars syntax.
- Links target the current Svelte application routes. Route and rendering behavior is covered by `MailerTests`.
- Shared layout, footer, metadata, and design tokens belong in `src/components`, `src/lib`, and `src/theme.ts`.

The build fails when the source, registry, and generated output sets are not one-to-one.
