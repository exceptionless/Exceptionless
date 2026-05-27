import { Renderer } from '@better-svelte-email/server';
import type { Component } from 'svelte';
import { writeFileSync, mkdirSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { tailwindTheme } from './theme.js';

// Import all templates
import UserPasswordReset from './templates/user-password-reset.svelte';
import UserEmailVerify from './templates/user-email-verify.svelte';
import EventNotice from './templates/event-notice.svelte';
import ProjectDailySummary from './templates/project-daily-summary.svelte';
import OrganizationAdded from './templates/organization-added.svelte';
import OrganizationInvited from './templates/organization-invited.svelte';
import OrganizationNotice from './templates/organization-notice.svelte';
import OrganizationPaymentFailed from './templates/organization-payment-failed.svelte';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Template registry: name → component
const templates: Record<string, Component> = {
    'user-password-reset': UserPasswordReset as unknown as Component,
    'user-email-verify': UserEmailVerify as unknown as Component,
    'event-notice': EventNotice as unknown as Component,
    'project-daily-summary': ProjectDailySummary as unknown as Component,
    'organization-added': OrganizationAdded as unknown as Component,
    'organization-invited': OrganizationInvited as unknown as Component,
    'organization-notice': OrganizationNotice as unknown as Component,
    'organization-payment-failed': OrganizationPaymentFailed as unknown as Component
};

function cleanHtml(html: string): string {
    // Remove all Svelte SSR comment markers
    html = html.replace(/<!--\[!?-->/g, '');
    html = html.replace(/<!--]-->/g, '');
    html = html.replace(/<!--\[-->/g, '');
    html = html.replace(/<!---->/g, '');

    // Strip HTML comments (e.g. <!-- prevent Gmail iOS ... -->) before output
    html = html.replace(/<!--[\s\S]*?-->/g, '');

    // Extract JSON-LD script blocks before whitespace collapsing.
    // Without this, adjacent JSON `}` chars can merge with Handlebars `}}` tokens.
    const scripts: string[] = [];
    html = html.replace(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g, (_match, content: string) => {
        scripts.push(content);
        return `__SCRIPT_PLACEHOLDER_${scripts.length - 1}__`;
    });

    // Collapse inter-tag whitespace
    html = html.replace(/>\s+</g, '><');
    html = html.replace(/\n\s*/g, '');

    // Restore script blocks preserving newlines so `}}` inside JSON
    // cannot be mis-parsed as Handlebars closing tokens.
    html = html.replace(/__SCRIPT_PLACEHOLDER_(\d+)__/g, (_match, idx: string) => {
        const content = scripts[parseInt(idx, 10)];
        return `<script type="application/ld+json">\n${content.trim()}\n</script>`;
    });

    return html.trim();
}

function validateTemplate(name: string, html: string): void {
    if (html.includes('&#123;') || html.includes('&lbrace;') || html.includes('&#x7B;')) {
        throw new Error(`Template "${name}" has HTML-encoded curly braces — Handlebars tokens are broken.`);
    }

    const blockOpens = (html.match(/\{\{#(if|each|unless)/g) ?? []).length;
    const blockCloses = (html.match(/\{\{\/(if|each|unless)/g) ?? []).length;
    if (blockOpens !== blockCloses) {
        throw new Error(
            `Template "${name}" has unbalanced Handlebars blocks: ${blockOpens} opens vs ${blockCloses} closes`
        );
    }

    const tokenOpens = (html.match(/\{\{/g) ?? []).length;
    const tokenCloses = (html.match(/\}\}/g) ?? []).length;
    if (tokenOpens !== tokenCloses) {
        throw new Error(
            `Template "${name}" has unbalanced Handlebars delimiters: ${tokenOpens} {{ vs ${tokenCloses} }}`
        );
    }

    if (!html.includes('<!DOCTYPE html')) {
        throw new Error(`Template "${name}" is missing DOCTYPE declaration`);
    }
    if (!html.includes('{{Subject}}')) {
        throw new Error(`Template "${name}" is missing required {{Subject}} token`);
    }
}

async function main(): Promise<void> {
    const renderer = new Renderer({ tailwindConfig: tailwindTheme });

    const outputDir = resolve(__dirname, '..', '..', 'Exceptionless.Core', 'Mail', 'Templates');
    mkdirSync(outputDir, { recursive: true });

    const names = Object.keys(templates);
    console.log(`Building ${names.length} email templates...`);

    for (const [name, component] of Object.entries(templates)) {
        console.log(`  Rendering: ${name}`);
        const raw = await renderer.render(component);
        const html = cleanHtml(raw);
        validateTemplate(name, html);
        writeFileSync(resolve(outputDir, `${name}.html`), html);
    }

    console.log(`\nDone! ${names.length} templates written to: ${outputDir}`);
}

main().catch((err: unknown) => {
    console.error('Build failed:', err);
    process.exit(1);
});
