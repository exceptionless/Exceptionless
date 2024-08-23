import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
    kit: {
        adapter: adapter({
            fallback: 'index.html'
        }),
        alias: {
            $comp: 'src/lib/features/shared/components',
            $features: 'src/lib/features',
            $generated: 'src/lib/generated',
            $lib: 'src/lib',
            $shared: 'src/lib/features/shared'
        },
        paths: {
            base: '/next'
        }
    },
    preprocess: vitePreprocess()
};

export default config;
