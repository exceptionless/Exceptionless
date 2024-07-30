import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
    kit: {
        adapter: adapter({
            fallback: 'index.html'
        }),
        alias: {
            $api: 'src/lib/api',
            $comp: 'src/lib/components',
            $lib: 'src/lib'
        },
        paths: {
            base: '/next'
        }
    },
    preprocess: vitePreprocess()
};

export default config;
