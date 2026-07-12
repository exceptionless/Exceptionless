import { svelte } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vite';

// Storybook loads the default Vite configuration. Keep production-template
// generation in vite.email.config.ts so preview builds cannot mutate Core files.
export default defineConfig({
    plugins: [svelte()]
});
