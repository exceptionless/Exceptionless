import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import { defineConfig } from 'vite';

const aspNetConfig = getAspNetConfig();

export default defineConfig({
    build: {
        sourcemap: true,
        target: 'esnext'
    },
    plugins: [tailwindcss(), sveltekit()],
    server: {
        hmr: aspNetConfig.hmr,
        host: true,
        port: parseInt(process.env.PORT ?? '5173'),
        proxy: {
            '/_framework': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.url
            },
            '/_vs': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.url
            },
            // proxy API requests to the ASP.NET backend
            '/api': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.url
            },
            '/api/v2/push': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.wsUrl,
                ws: true
            },
            '/docs': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.url
            },
            '/health': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.url
            },
            '/ready': {
                changeOrigin: true,
                secure: false,
                target: aspNetConfig.url
            },
            '^/(?!(next|api|docs|health|ready|_)).*': {
                changeOrigin: true,
                secure: false,
                target: 'http://localhost:5100'
            }
        },
        strictPort: true
    },
    test: {
        workspace: [
            {
                extends: './vite.config.ts',
                plugins: [svelteTesting()],

                test: {
                    clearMocks: true,
                    environment: 'jsdom',
                    exclude: ['src/lib/server/**'],
                    include: ['src/**/*.svelte.{test,spec}.{js,ts}'],
                    name: 'client',
                    setupFiles: ['./vitest-setup-client.ts']
                }
            },
            {
                extends: './vite.config.ts',

                test: {
                    environment: 'node',
                    exclude: ['src/**/*.svelte.{test,spec}.{js,ts}'],
                    include: ['src/**/*.{test,spec}.{js,ts}'],
                    name: 'server'
                }
            }
        ]
    }
});

// adapted from src/setupProxy.js in ASP.NET React template
function getAspNetConfig() {
    // check to see if we are running inside of codespaces
    const codespaceName = process.env.CODESPACE_NAME;
    const codespaceDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN;

    // get current aspnetcore port / url
    const aspnetHttpsPort = process.env.ASPNETCORE_HTTPS_PORT;
    const aspnetUrls = process.env.ASPNETCORE_URLS ?? process.env.services__Api__0;
    const serverPort = 5173;

    const hmrRemoteHost = codespaceName ? `${codespaceName}-${serverPort}.${codespaceDomain}` : 'localhost';
    const hmrRemotePort = codespaceName ? 443 : serverPort;

    let url = 'http://localhost:5200';
    if (aspnetHttpsPort) {
        url = `https://localhost:${aspnetHttpsPort}`;
    } else if (aspnetUrls) {
        url = aspnetUrls.split(';')[0] as string;
    }

    const wsUrl = url.replace('https://', 'wss://').replace('http://', 'ws://');

    let hmrRemoteProtocol = 'ws';
    if (codespaceName || (wsUrl.startsWith('wss') && hmrRemoteHost !== 'localhost')) {
        hmrRemoteProtocol = 'wss';
    }

    return {
        hmr: {
            clientPort: hmrRemotePort,
            host: hmrRemoteHost,
            port: hmrRemotePort,
            protocol: hmrRemoteProtocol
        },
        url,
        wsUrl
    };
}
