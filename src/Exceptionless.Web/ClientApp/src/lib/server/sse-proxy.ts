import type { ProxyOptions } from 'vite';

export function createSseProxy(target: string | undefined): ProxyOptions {
    return {
        changeOrigin: true,
        fetchOptions: {
            onBeforeRequest(options, _request, response) {
                // Vite's HTTP/2 proxy uses fetch; closing its response must also
                // cancel the upstream SSE request so the API releases its lease.
                const controller = new AbortController();
                response.once('close', () => controller.abort());
                if (response.destroyed) {
                    controller.abort();
                }
                options.signal = options.signal ? AbortSignal.any([options.signal, controller.signal]) : controller.signal;
            }
        },
        target,
        ws: true
    };
}
