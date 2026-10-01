import { svelte } from '@sveltejs/vite-plugin-svelte';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
    plugins: [svelte()],
    resolve: { conditions: ['browser'] },
    root: fileURLToPath(new URL('.', import.meta.url)),
    test: {
        coverage: {
            exclude: ['**/*.test.ts'],
            include: ['src/**/*.{ts,svelte}'],
            provider: 'istanbul',
            reporter: ['json']
        },
        environment: 'jsdom',
        include: ['src/**/*.test.ts'],
        maxWorkers: 1
    }
});
