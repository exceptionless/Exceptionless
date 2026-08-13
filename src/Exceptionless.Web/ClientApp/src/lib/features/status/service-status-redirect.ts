const DEFAULT_HEALTHY_CACHE_MILLISECONDS = 5000;

interface ServiceStatusRedirectorOptions {
    checkHealth: () => Promise<boolean>;
    healthyCacheMilliseconds?: number;
    navigate: () => Promise<void>;
    now?: () => number;
}

export function buildServiceStatusUrl(statusPath: string, currentUrl: Pick<URL, 'hash' | 'pathname' | 'search'>): string {
    const redirect = `${currentUrl.pathname}${currentUrl.search}${currentUrl.hash}`;
    return `${statusPath}?${new URLSearchParams({ redirect }).toString()}`;
}

export function createServiceStatusRedirector(options: ServiceStatusRedirectorOptions): () => Promise<void> {
    const healthyCacheMilliseconds = options.healthyCacheMilliseconds ?? DEFAULT_HEALTHY_CACHE_MILLISECONDS;
    const now = options.now ?? Date.now;
    let healthCheckPromise: null | Promise<boolean> = null;
    let healthyUntil = 0;
    let navigationPromise: null | Promise<void> = null;

    async function checkHealth(): Promise<boolean> {
        try {
            return await options.checkHealth();
        } catch {
            return false;
        } finally {
            healthCheckPromise = null;
        }
    }

    return async () => {
        if (now() < healthyUntil) {
            return;
        }

        healthCheckPromise ??= checkHealth();
        const isHealthy = await healthCheckPromise;
        if (isHealthy) {
            healthyUntil = now() + healthyCacheMilliseconds;
            return;
        }

        navigationPromise ??= options.navigate().finally(() => {
            navigationPromise = null;
        });
        await navigationPromise;
    };
}
