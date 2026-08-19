<script lang="ts">
    import type { Snippet } from 'svelte';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as Sidebar from '$comp/ui/sidebar';
    import { Toaster } from '$comp/ui/sonner';
    import { accessToken } from '$features/auth/index.svelte';
    import { handleUnexpectedUnauthorized } from '$features/auth/unauthorized';
    import { buildServiceStatusUrl, createServiceStatusRedirector, isServiceUnavailableStatus } from '$features/status/service-status-redirect';
    import { type FetchClientContext, ProblemDetails, setAccessTokenFunc, setBaseUrl, setRequestOptions, useMiddleware } from '@foundatiofx/fetchclient';
    import { error } from '@sveltejs/kit';
    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';

    import '../app.css';

    import { ModeWatcher } from 'mode-watcher';

    import { routes } from './routes.svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();

    setBaseUrl('api/v2');
    setRequestOptions({
        errorCallback: (response) => {
            throw response.problem ?? response;
        },
        timeout: 5000
    });
    setAccessTokenFunc(() => accessToken.current);

    const redirectToServiceStatus = createServiceStatusRedirector({
        checkHealth: async () => {
            const response = await fetch('/health', {
                cache: 'no-store',
                signal: AbortSignal.timeout(5000)
            });
            return response.ok;
        },
        navigate: async () => {
            const url = page.url;
            if (url.pathname.startsWith(resolve('/status'))) {
                return;
            }

            await goto(buildServiceStatusUrl(resolve('/status'), url), {
                replaceState: true
            });
        }
    });

    useMiddleware(async (ctx: FetchClientContext, next: () => Promise<void>) => {
        await next();

        const status = ctx.response?.status;
        if (status === undefined) {
            return;
        }

        if (handleUnexpectedUnauthorized(status, ctx.options.expectedStatusCodes)) {
            return;
        } else if (status === 404 && !ctx.options.expectedStatusCodes?.includes(404)) {
            throw error(404, 'Not found');
        } else if (isServiceUnavailableStatus(status) && !ctx.options.expectedStatusCodes?.includes(status)) {
            if (page.url.pathname.startsWith(resolve('/status'))) {
                return;
            }

            await redirectToServiceStatus();
        }
    });

    const managedTitlePrefixes = [resolve('/(app)/stack'), resolve('/(app)/event'), resolve('/(app)/stream')];

    $effect(() => {
        // Skip title for pages that manage their own (stacks, events, stream with saved views)
        if (managedTitlePrefixes.some((prefix) => page.url.pathname === prefix || page.url.pathname.startsWith(prefix + '/'))) {
            return;
        }

        const currentRoute = routes().find((route) => page.url.pathname === route.href);
        if (currentRoute) {
            document.title = `${currentRoute.title} - Exceptionless`;
        } else {
            document.title = 'Exceptionless';
        }
    });

    const queryClient = new QueryClient({
        defaultOptions: {
            queries: {
                retry: (failureCount, error) => {
                    if (failureCount > 2) {
                        return false;
                    }

                    if (error instanceof ProblemDetails) {
                        const status = error.status;

                        // Never retry auth / obvious client bugs
                        if (!status) {
                            return true;
                        }

                        if ([400, 401, 403, 404, 410, 422, 426].includes(status)) {
                            return false;
                        }

                        // Retry "likely transient" errors
                        if (status === 408 || status === 429 || (status >= 500 && status < 600)) {
                            return true;
                        }

                        // Default: no retry
                        return false;
                    }

                    return true;
                },
                staleTime: 5 * 60 * 1000
            }
        }
    });
</script>

<div class="bg-background text-foreground">
    <ModeWatcher defaultMode="dark" />

    <QueryClientProvider client={queryClient}>
        <Sidebar.Provider>
            {@render children()}
        </Sidebar.Provider>

        <SvelteQueryDevtools />
    </QueryClientProvider>

    <Toaster position="bottom-right" />
</div>
