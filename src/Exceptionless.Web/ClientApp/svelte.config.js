import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
    compilerOptions: {
        experimental: {
            async: false
        }
    },
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
        // Native CSP covers Vite responses and hashes the static SPA bootstrap at build time.
        csp: {
            directives: {
                'base-uri': ['none'],
                // ws: also permits wss:; * alone covers HTTP(S), not WebSockets.
                'connect-src': ['*', 'ws:'],
                'default-src': ['self'],
                'font-src': ['self', 'https://*.intercomcdn.com'],
                'form-action': ['self'],
                'frame-ancestors': ['none'],
                'frame-src': ['self', 'https://*.stripe.com', 'https://link.com', 'https://*.link.com'],
                'img-src': [
                    'self',
                    'blob:',
                    'data:',
                    'https://*.link.com',
                    'https://js.intercomcdn.com',
                    'https://static.intercomassets.com',
                    'https://www.gravatar.com'
                ],
                'manifest-src': ['self'],
                'media-src': ['self', 'https://js.intercomcdn.com'],
                'object-src': ['none'],
                'script-src': ['self', 'strict-dynamic', 'https://*.stripe.com', 'https://*.intercom.io', 'https://js.intercomcdn.com'],
                'style-src': ['self', 'unsafe-inline'],
                'upgrade-insecure-requests': process.env.NODE_ENV === 'production',
                'worker-src': ['self']
            },
            mode: 'auto'
        }
    },
    preprocess: vitePreprocess()
};

export default config;
