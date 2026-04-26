import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import { defineConfig } from 'vitest/config';

const apiTarget = process.env.API_HTTPS || process.env.API_HTTP;
const apiProxy = { changeOrigin: true, target: apiTarget };

const oldAppTarget = process.env.OLDAPP_HTTPS || process.env.OLDAPP_HTTP;
const oldAppProxy = { changeOrigin: true, secure: false, target: oldAppTarget };

const codespaceName = process.env.CODESPACE_NAME;
const codespaceDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN;
const hmr = codespaceName && codespaceDomain ? { clientPort: 443, host: `${codespaceName}-5173.${codespaceDomain}`, protocol: 'wss' as const } : undefined;

export default defineConfig({
    base: '/next/',
    clearScreen: false,
    logLevel: 'info',
    build: {
        sourcemap: true,
        target: 'esnext'
    },
    optimizeDeps: {
        entries: ['src/**/*.{svelte,ts,js}']
    },
    plugins: [tailwindcss(), sveltekit()],
    server: {
        hmr,
        warmup: {
            clientFiles: ['src/routes/**/*.svelte']
        },
        proxy: {
            '/api': { ...apiProxy, ws: true },
            '/docs': apiProxy,
            '/health': apiProxy,
            '/ready': apiProxy,
            '^/(?!(next|api|docs|health|ready|_)).*': oldAppProxy
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
