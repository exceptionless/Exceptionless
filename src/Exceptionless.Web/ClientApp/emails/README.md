# Exceptionless Email Templates

These Svelte 5 sources are an additional build target in the existing ClientApp package. They generate the Handlebars HTML consumed by `Exceptionless.Core`; there is no separate email application or dependency lockfile.

```bash
npm ci
npm run build:emails
npm run dev:emails
```

Every `emails/templates/*.svelte` source must produce exactly one same-named file in `Exceptionless.Core/Mail/Templates`. The build validates that one-to-one inventory, the HTML doctype, and the required Handlebars subject token. `MailerTests` validates rendered content, application URLs, external links, and JSON-LD actions.

Keep shared email-client resets in `EmailLayout.svelte`. Template-specific compatibility styles belong to the template that renders them; footer styles belong to their footer component. Email templates reuse the application theme from `src/app.css` through the renderer's `customCSS` option.
