import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/kit/vite';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	preprocess: vitePreprocess(),
	kit: {
		paths: {
			base: '/next'
		},
		adapter: adapter({
			fallback: 'index.html'
		}),
		alias: {
			$api: 'src/lib/api',
			$comp: 'src/lib/components'
		}
	}
};

export default config;
