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
    let healthyUntil = 0;
    let redirectPromise: null | Promise<void> = null;

    async function redirect(): Promise<void> {
        try {
            if (await options.checkHealth()) {
                healthyUntil = now() + healthyCacheMilliseconds;
                return;
            }
        } catch {
            // A failed probe means the service cannot be reached.
        }

        await options.navigate();
    }

    return () => {
        if (now() < healthyUntil) {
            return Promise.resolve();
        }

        redirectPromise ??= redirect().finally(() => {
            redirectPromise = null;
        });
        return redirectPromise;
    };
}
