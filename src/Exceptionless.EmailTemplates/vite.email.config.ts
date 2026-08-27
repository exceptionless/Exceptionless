import { execSync } from 'node:child_process';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vite';

export default defineConfig({
    plugins: [
        svelte(),
        {
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
