import type { Component } from 'svelte';

import { Renderer } from '@better-svelte-email/server';
import { mkdir, readdir, writeFile } from 'node:fs/promises';
import { basename, resolve } from 'node:path';

import { tailwindTheme } from './theme';

const templateModules = import.meta.glob('./templates/*.svelte', {
    eager: true,
    import: 'default'
}) as Record<string, Component>;

const svelteRenderMarkers = ['<!---->', '<!--[-->', '<!--[-1-->', '<!--]-->'];

async function buildEmails(): Promise<void> {
    const renderer = new Renderer({ tailwindConfig: tailwindTheme });
    const outputDirectory = resolve(process.cwd(), '..', '..', 'Exceptionless.Core', 'Mail', 'Templates');
    const sourceDirectory = resolve(process.cwd(), 'emails', 'templates');
    const templates = Object.entries(templateModules)
        .map(([path, component]) => [basename(path, '.svelte'), component] as const)
        .sort(([left], [right]) => left.localeCompare(right));
    const templateNames = templates.map(([name]) => name);

    validateTemplateNames(templateNames, await getTemplateNames(sourceDirectory, '.svelte'), 'emails/templates');

    const renderedTemplates = await Promise.all(
        templates.map(async ([name, component]) => {
            const html = normalizeRendererOutput(await renderer.render(component));
            validateTemplate(name, html);
            return { html, name };
        })
    );

    await mkdir(outputDirectory, { recursive: true });
    await Promise.all(renderedTemplates.map(({ html, name }) => writeFile(resolve(outputDirectory, `${name}.html`), html)));
    validateTemplateNames(templateNames, await getTemplateNames(outputDirectory, '.html'), 'Exceptionless.Core/Mail/Templates');

    console.log(`Rendered ${templateNames.length} email templates.`);
}

async function getTemplateNames(directory: string, extension: string): Promise<string[]> {
    const entries = await readdir(directory, { withFileTypes: true });
    return entries
        .filter((entry) => entry.isFile() && entry.name.endsWith(extension))
        .map((entry) => entry.name.slice(0, -extension.length))
        .sort();
}

function normalizeRendererOutput(html: string): string {
    let normalized = html.replaceAll(' target="_blank"', '');
    for (const marker of svelteRenderMarkers) {
        normalized = normalized.replaceAll(marker, '');
    }

    return normalized.trim();
}

function validateTemplate(name: string, html: string): void {
    if (html.includes('&#123;') || html.includes('&lbrace;') || html.includes('&#x7B;')) {
        throw new Error(`Template "${name}" has HTML-encoded curly braces.`);
    }

    if (!html.includes('<!DOCTYPE html')) {
        throw new Error(`Template "${name}" is missing its doctype.`);
    }

    if (!html.includes('{{Subject}}')) {
        throw new Error(`Template "${name}" is missing the required {{Subject}} token.`);
    }

    if (svelteRenderMarkers.some((marker) => html.includes(marker))) {
        throw new Error(`Template "${name}" contains an unexpected Svelte render marker.`);
    }
}

function validateTemplateNames(expected: string[], actual: string[], location: string): void {
    if (JSON.stringify(expected) !== JSON.stringify(actual)) {
        throw new Error(`${location} must contain exactly: ${expected.join(', ')}. Found: ${actual.join(', ')}.`);
    }
}

await buildEmails();
