<script lang="ts">
    import type { Snippet } from 'svelte';

    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import * as Sidebar from '$comp/ui/sidebar';
    import { Toaster } from '$comp/ui/sonner';
    import { accessToken } from '$features/auth/index.svelte';
    import { type FetchClientContext, ProblemDetails, setAccessTokenFunc, setBaseUrl, setRequestOptions, useMiddleware } from '@exceptionless/fetchclient';
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

    useMiddleware(async (ctx: FetchClientContext, next: () => Promise<void>) => {
        await next();

        const status = ctx.response?.status;
        if (status === undefined) {
            return;
        }

        if (status === 404 && !ctx.options.expectedStatusCodes?.includes(404)) {
            throw error(404, 'Not found');
        }

        if (([0, 408].includes(status) || status >= 500) && !ctx.options.expectedStatusCodes?.includes(status)) {
            const url = page.url;
            if (url.pathname.startsWith('/next/status')) {
                return;
            }

            await goto(`/next/status?redirect=${url.pathname}`, { replaceState: true });
        }
    });

    $effect(() => {
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
                        return !!error.status && error.status < 500;
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
