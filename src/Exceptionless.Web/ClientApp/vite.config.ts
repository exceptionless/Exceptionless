import type { Plugin } from 'vite';

import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import MagicString from 'magic-string';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { defineConfig } from 'vitest/config';

const apiTarget = process.env.API_HTTPS || process.env.API_HTTP;
const apiProxy = { changeOrigin: true, target: apiTarget };

const oldAppTarget = process.env.OLDAPP_HTTPS || process.env.OLDAPP_HTTP;
const oldAppProxy = { changeOrigin: true, secure: false, target: oldAppTarget };

const port = Number(process.env.PORT) || 7131;
const codespaceName = process.env.CODESPACE_NAME;
const codespaceDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN;
const hmr = codespaceName && codespaceDomain ? { clientPort: 443, host: `${codespaceName}-${port}.${codespaceDomain}`, protocol: 'wss' as const } : undefined;
const allowedHosts = ['web-ex.dev.localhost', 'localhost', '127.0.0.1'];
if (codespaceName && codespaceDomain) {
    allowedHosts.push(`${codespaceName}-${port}.${codespaceDomain}`);
}

const SVELTE_RUNTIME_DIAGNOSTICS_GLOBAL = '__exceptionlessSvelteEffectDepthDiagnostics';

// Svelte's useful effect-depth diagnostics are development-only. Preserve the
// minimal state-write tracking needed to diagnose production-only loops.
function svelteEffectDepthDiagnostics(): Plugin {
    const require = createRequire(import.meta.url);
    const packagePath = require.resolve('svelte/package.json');
    const packageDirectory = dirname(packagePath);
    const sourcesPath = join(packageDirectory, 'src/internal/client/reactivity/sources.js');
    const batchPath = join(packageDirectory, 'src/internal/client/reactivity/batch.js');

    const stateUpdateNeedle = '\tif (!source.equals(value)) {\n';
    const stateUpdateReplacement = `${stateUpdateNeedle}\t\tglobalThis.${SVELTE_RUNTIME_DIAGNOSTICS_GLOBAL}?.recordStateUpdate(source);\n`;
    const flushStartNeedle = '\tflush() {\n\t\ttry {\n';
    const flushStartReplacement = `\tflush() {\n\t\tglobalThis.${SVELTE_RUNTIME_DIAGNOSTICS_GLOBAL}?.startFlush();\n\n\t\ttry {\n`;
    const flushEndNeedle = '\t\t} finally {\n\t\t\tflush_count = 0;\n';
    const flushEndReplacement = `\t\t} finally {\n\t\t\tglobalThis.${SVELTE_RUNTIME_DIAGNOSTICS_GLOBAL}?.endFlush();\n\t\t\tflush_count = 0;\n`;
    const effectDepthNeedle = '\t} catch (error) {\n\t\tif (DEV) {\n\t\t\t// stack contains no useful information, replace it\n';
    const effectDepthReplacement = `\t} catch (error) {\n\t\tglobalThis.${SVELTE_RUNTIME_DIAGNOSTICS_GLOBAL}?.attachEffectDepthError(error, last_scheduled_effect);\n\n\t\tif (DEV) {\n\t\t\t// stack contains no useful information, replace it\n`;

    function findRuntimePatchTarget(code: string, id: string, needle: string) {
        const start = code.indexOf(needle);
        if (start < 0) {
            throw new Error(`Unable to instrument ${id}; the expected Svelte runtime source was not found.`);
        }

        if (code.indexOf(needle, start + needle.length) >= 0) {
            throw new Error(`Unable to instrument ${id}; the expected Svelte runtime source was found more than once.`);
        }

        return start;
    }

    function instrumentRuntime(code: string, id: string, replacements: [needle: string, replacement: string][]) {
        const instrumented = new MagicString(code, { filename: id });
        for (const [needle, replacement] of replacements) {
            const start = findRuntimePatchTarget(code, id, needle);
            instrumented.overwrite(start, start + needle.length, replacement);
        }

        return {
            code: instrumented.toString(),
            map: instrumented.generateMap({ hires: true, includeContent: true, source: id })
        };
    }

    function assertRuntimeShape() {
        const sources = readFileSync(sourcesPath, 'utf8');
        const batch = readFileSync(batchPath, 'utf8');
        findRuntimePatchTarget(sources, sourcesPath, stateUpdateNeedle);
        findRuntimePatchTarget(batch, batchPath, flushStartNeedle);
        findRuntimePatchTarget(batch, batchPath, flushEndNeedle);
        findRuntimePatchTarget(batch, batchPath, effectDepthNeedle);
    }

    return {
        apply: 'build',
        configResolved: assertRuntimeShape,
        enforce: 'pre',
        name: 'exceptionless-svelte-effect-depth-diagnostics',
        transform(code, id) {
            const path = id.split('?', 1)[0]?.replaceAll('\\', '/');
            if (path?.endsWith('/svelte/src/internal/client/reactivity/sources.js')) {
                return instrumentRuntime(code, id, [[stateUpdateNeedle, stateUpdateReplacement]]);
            }

            if (path?.endsWith('/svelte/src/internal/client/reactivity/batch.js')) {
                return instrumentRuntime(code, id, [
                    [flushStartNeedle, flushStartReplacement],
                    [flushEndNeedle, flushEndReplacement],
                    [effectDepthNeedle, effectDepthReplacement]
                ]);
            }
        }
    };
}

function svelteKitRuntimeDefines(): Plugin {
    let replacements = new Map<string, string>();

    return {
        apply: 'serve',
        configResolved(config) {
            replacements = new Map(
                Object.entries(config.define ?? {})
                    .filter(([key]) => key.startsWith('__SVELTEKIT_'))
                    .map(([key, value]) => [key, String(value)])
            );
        },
        enforce: 'pre',
        name: 'exceptionless-sveltekit-runtime-defines',
        transform(code, id) {
            if (!id.includes('/node_modules/@sveltejs/kit/src/runtime/') && !id.includes('\\node_modules\\@sveltejs\\kit\\src\\runtime\\')) {
                return;
            }

            let transformed = code;
            for (const [key, value] of replacements) {
                transformed = transformed.replaceAll(key, value);
            }

            return transformed === code ? undefined : { code: transformed, map: null };
        }
    };
}

export default defineConfig({
    build: {
        sourcemap: true,
        target: 'esnext'
    },
    clearScreen: false,
    logLevel: 'info',
    plugins: [tailwindcss(), sveltekit(), svelteKitRuntimeDefines(), svelteEffectDepthDiagnostics()],
    server: {
        allowedHosts,
        hmr,
        port,
        proxy: {
            '/api': { ...apiProxy, ws: true },
            '/docs': apiProxy,
            '/health': apiProxy,
            '/ready': apiProxy,
            '^/(?!(next|api|docs|health|ready|_)).*': oldAppProxy
        },
        strictPort: true,
        warmup: {
            clientFiles: ['src/routes/**/*.svelte']
        }
    },
    test: {
        projects: [
            {
                extends: './vite.config.ts',
                plugins: [svelteTesting()],

                test: {
                    clearMocks: true,
                    environment: 'jsdom',
                    exclude: ['src/lib/server/**'],
                    include: ['src/**/*.svelte.{test,spec}.{js,ts}'],
                    name: 'client',
                    setupFiles: ['./vitest-setup-client.ts']
                }
            },
            {
                extends: './vite.config.ts',

                test: {
                    environment: 'node',
                    exclude: ['src/**/*.svelte.{test,spec}.{js,ts}'],
                    include: ['src/**/*.{test,spec}.{js,ts}'],
                    name: 'server',
                    setupFiles: ['./vitest-setup-server.ts']
                }
            }
        ]
    }
});
