import { execSync } from 'node:child_process';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vite';

export default defineConfig({
    plugins: [
        svelte(),
        {
            // After each Vite build (including watch-mode rebuilds), run the
            // email renderer so the generated HTML files in
            // Exceptionless.Core/Mail/Templates stay in sync with source changes.
            name: 'run-email-renderer',
            closeBundle() {
                execSync('node dist/build.js', { stdio: 'inherit' });
            }
        }
    ],
    build: {
        ssr: true,
        target: 'node20',
        outDir: 'dist',
        rollupOptions: {
            input: 'src/build-emails.ts',
            output: {
                format: 'esm',
                entryFileNames: 'build.js'
            }
        }
    }
});
