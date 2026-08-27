import { Renderer } from '@better-svelte-email/server';
import Handlebars from 'handlebars';
import type { Component } from 'svelte';
import { existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { tailwindTheme } from './theme.js';

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
    html = html.replace(/<!--\[!?-->/g, '');
    html = html.replace(/<!--]-->/g, '');
    html = html.replace(/<!--\[-->/g, '');
    html = html.replace(/<!--\[-?\d*-->/g, '');
    html = html.replace(/<!---->/g, '');
    // Extract JSON-LD script blocks before whitespace collapsing — adjacent `}` chars
    // can merge into `}}` after collapse, which HandlebarsDotNet parses as a closing token.
    const scripts: string[] = [];
    html = html.replace(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g, (_match, content: string) => {
        scripts.push(content);
        return `__SCRIPT_PLACEHOLDER_${scripts.length - 1}__`;
    });

    html = html.replace(/>\s+</g, '><');
    // Replace newlines within text nodes with a space (not empty string) to prevent
    // adjacent words from being concatenated (e.g. "from\nwhich" → "from which").
    html = html.replace(/\n[ \t]*/g, ' ');
    html = html.replace(/ {2,}/g, ' ');

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

    try {
        Handlebars.parse(html);
    } catch (error) {
        throw new Error(`Template "${name}" has invalid Handlebars syntax`, { cause: error });
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

    const entries = Object.entries(templates);
    const names = entries.map(([name]) => name);
    console.log(`Building ${names.length} email templates...`);

    const renderedTemplates = await Promise.all(
        entries.map(async ([name, component]) => {
            console.log(`  Rendering: ${name}`);
            const raw = await renderer.render(component);
            const html = cleanHtml(raw);
            validateTemplate(name, html);
            return { name, html };
        })
    );

    const manifestPath = resolve(__dirname, '..', 'generated-templates.json');
    const previousNames: string[] = existsSync(manifestPath) ? JSON.parse(readFileSync(manifestPath, 'utf8')) : [];
    for (const staleName of previousNames.filter((name) => !names.includes(name))) {
        const stalePath = resolve(outputDir, `${staleName}.html`);
        if (existsSync(stalePath)) {
            unlinkSync(stalePath);
        }
    }

    for (const { name, html } of renderedTemplates) {
        writeFileSync(resolve(outputDir, `${name}.html`), html);
    }

    writeFileSync(manifestPath, `${JSON.stringify(names.slice().sort(), null, 4)}\n`);

    console.log(`\nDone! ${names.length} templates written to: ${outputDir}`);
}

main().catch((err: unknown) => {
    console.error('Build failed:', err);
    process.exit(1);
});
