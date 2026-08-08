import { svelte } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vite';

export default defineConfig({
    plugins: [svelte()],
    build: {
        ssr: true,
        target: 'node20',
        outDir: 'dist',
        emptyOutDir: false,
        rollupOptions: {
            input: 'src/validate-parity.ts',
            output: {
                format: 'esm',
                entryFileNames: 'validate-parity.js'
            }
        }
    }
});
