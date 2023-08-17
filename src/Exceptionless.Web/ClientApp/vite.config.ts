import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vitest/config';
import Icons from 'unplugin-icons/vite';

const aspNetConfig = getAspNetConfig();

export default defineConfig({
	plugins: [
		sveltekit(),
		Icons({
			compiler: 'svelte'
		})
	],
	test: {
		include: ['src/**/*.{test,spec}.{js,ts}']
	},
	server: {
		port: 5173,
		strictPort: true,
		hmr: aspNetConfig.hmr,
		proxy: {
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
			'/_framework': {
				changeOrigin: true,
				secure: false,
				target: aspNetConfig.url
			},
			'/_vs': {
				changeOrigin: true,
				secure: false,
				target: aspNetConfig.url
			}
		}
	}
});

// adapted from src/setupProxy.js in ASP.NET React template
function getAspNetConfig() {
	// check to see if we are running inside of codespaces
	const codespaceName = process.env.CODESPACE_NAME;
	const codespaceDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN;

	// get current aspnetcore port / url
	const aspnetHttpsPort = process.env.ASPNETCORE_HTTPS_PORT;
	const aspnetUrls = process.env.ASPNETCORE_URLS;
	const serverPort = 5173;

	const hmrRemoteHost = codespaceName
		? `${codespaceName}-${serverPort}.${codespaceDomain}`
		: 'localhost';
	const hmrRemotePort = codespaceName ? 443 : serverPort;

	let url = 'http://localhost:5292';
	if (aspnetHttpsPort) {
		url = `https://localhost:${aspnetHttpsPort}`;
	} else if (aspnetUrls) {
		url = aspnetUrls.split(';')[0];
	}

	const wsUrl = url.replace('https://', 'wss://').replace('http://', 'ws://');
	const hmrRemoteProtocol = codespaceName ? 'wss' : wsUrl.startsWith('wss') ? 'wss' : 'ws';

	return {
		url,
		wsUrl,
		hmr: {
			protocol: hmrRemoteProtocol,
			host: hmrRemoteHost,
			port: hmrRemotePort,
			clientPort: hmrRemotePort
		}
	};
}
