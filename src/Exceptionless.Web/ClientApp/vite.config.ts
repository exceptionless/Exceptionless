import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';

const apiTarget = process.env.API_HTTPS || process.env.API_HTTP;
const apiProxy = { changeOrigin: true, target: apiTarget };

const oldAppTarget = process.env.OLDAPP_HTTPS || process.env.OLDAPP_HTTP;
const oldAppProxy = { changeOrigin: true, secure: false, target: oldAppTarget };

const codespaceName = process.env.CODESPACE_NAME;
const codespaceDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN;
const hmr = codespaceName && codespaceDomain ? { clientPort: 443, host: `${codespaceName}-7131.${codespaceDomain}`, protocol: 'wss' as const } : undefined;
const allowedHosts = ['web-ex.dev.localhost', 'localhost', '127.0.0.1'];
if (codespaceName && codespaceDomain) {
    allowedHosts.push(`${codespaceName}-7131.${codespaceDomain}`);
}

function svelteKitRuntimeDefines(): Plugin {
    let replacements = new Map<string, string>();

    return {
        apply: 'serve',
        enforce: 'pre',
        name: 'exceptionless-sveltekit-runtime-defines',
        configResolved(config) {
            replacements = new Map(
                Object.entries(config.define ?? {})
                    .filter(([key]) => key.startsWith('__SVELTEKIT_'))
                    .map(([key, value]) => [key, String(value)])
            );
        },
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
    plugins: [tailwindcss(), sveltekit(), svelteKitRuntimeDefines()],
    server: {
        allowedHosts,
        hmr,
        port: 7131,
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
