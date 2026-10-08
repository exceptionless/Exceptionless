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
        // The published build must be served through ASP.NET: its header restricts these
        // runtime/Vite connection targets and supplies frame-ancestors, which meta cannot.
        csp: {
            directives: {
                'base-uri': ['none'],
                // ws: also permits wss:; * alone covers HTTP(S), not WebSockets.
                'connect-src': ['*', 'ws:'],
                'default-src': ['none'],
                'font-src': ['self', 'https://*.intercomcdn.com'],
                'form-action': ['self'],
                'frame-src': ['https://*.stripe.com', 'https://link.com', 'https://*.link.com'],
                'img-src': [
                    'self',
                    'blob:',
                    'data:',
                    'https://*.link.com',
                    'https://js.intercomcdn.com',
                    'https://static.intercomassets.com',
                    'https://www.gravatar.com'
                ],
                'media-src': ['https://js.intercomcdn.com'],
                'object-src': ['none'],
                'script-src': ['self', 'strict-dynamic', 'https://*.stripe.com', 'https://*.intercom.io', 'https://js.intercomcdn.com'],
                'style-src': ['self', 'unsafe-inline'],
                'upgrade-insecure-requests': process.env.NODE_ENV === 'production',
                // Explicitly deny workers; without this, worker-src falls back to script-src.
                'worker-src': ['none']
            },
            mode: 'auto'
        }
    },
    preprocess: vitePreprocess()
};

export default config;
