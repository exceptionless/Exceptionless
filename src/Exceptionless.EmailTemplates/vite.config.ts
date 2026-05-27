import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [
    svelte({
      compilerOptions: {
        generate: 'server'
      }
    })
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
