import { svelte } from '@sveltejs/vite-plugin-svelte';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { defineConfig } from 'vite';

export default defineConfig({
    build: {
        emptyOutDir: true,
        outDir: '.email-dist',
        rollupOptions: {
            input: 'emails/build-emails.ts',
            output: {
                entryFileNames: 'build.js',
                format: 'esm'
            }
        },
        ssr: true,
        target: 'node24'
    },
    plugins: [
        svelte(),
        {
            name: 'render-email-templates',
            async writeBundle() {
                const renderer = pathToFileURL(resolve('.email-dist/build.js'));
                await import(`${renderer.href}?updated=${Date.now()}`);
            }
        }
    ]
});
