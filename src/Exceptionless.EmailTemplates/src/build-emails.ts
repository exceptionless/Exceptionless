import { Renderer } from '@better-svelte-email/server';
import Handlebars from 'handlebars';
import { mkdirSync, readdirSync, writeFileSync } from 'fs';
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
import ContactRequest from './templates/contact-request.svelte';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const templates = {
    'contact-request': ContactRequest,
    'user-password-reset': UserPasswordReset,
    'user-email-verify': UserEmailVerify,
    'event-notice': EventNotice,
    'project-daily-summary': ProjectDailySummary,
    'organization-added': OrganizationAdded,
    'organization-invited': OrganizationInvited,
    'organization-notice': OrganizationNotice,
    'organization-payment-failed': OrganizationPaymentFailed
};

function getTemplateNames(directory: string, extension: string): string[] {
    return readdirSync(directory, { withFileTypes: true })
        .filter((entry) => entry.isFile() && entry.name.endsWith(extension))
        .map((entry) => entry.name.slice(0, -extension.length))
        .sort();
}

function validateTemplateNames(expected: string[], actual: string[], location: string): void {
    if (JSON.stringify(expected) !== JSON.stringify(actual)) {
        throw new Error(`${location} must contain exactly: ${expected.join(', ')}. Found: ${actual.join(', ')}.`);
    }
}

function cleanHtml(html: string): string {
    html = html.replace(/<!--[\da-z]+-->/gi, '');
    html = html.replace(/<!--\[!?-->/g, '');
    html = html.replace(/<!--]-->/g, '');
    html = html.replace(/<!--\[-->/g, '');
    html = html.replace(/<!--\[-?\d*-->/g, '');
    html = html.replace(/<!---->/g, '');
    const scripts: string[] = [];
    html = html.replace(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g, (_match, content: string) => {
        scripts.push(content);
        return `__SCRIPT_PLACEHOLDER_${scripts.length - 1}__`;
    });

    html = html.replace(/>\s+</g, '><');
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
    const sourceDirectory = resolve(__dirname, '..', 'src', 'templates');
    mkdirSync(outputDir, { recursive: true });

    const entries = Object.entries(templates);
    const names = entries.map(([name]) => name).sort();
    validateTemplateNames(names, getTemplateNames(sourceDirectory, '.svelte'), 'src/templates');
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

    for (const { name, html } of renderedTemplates) {
        writeFileSync(resolve(outputDir, `${name}.html`), html);
    }

    validateTemplateNames(names, getTemplateNames(outputDir, '.html'), 'Exceptionless.Core/Mail/Templates');

    console.log(`\nDone! ${names.length} templates written to: ${outputDir}`);
}

main().catch((err: unknown) => {
    console.error('Build failed:', err);
    process.exit(1);
});
