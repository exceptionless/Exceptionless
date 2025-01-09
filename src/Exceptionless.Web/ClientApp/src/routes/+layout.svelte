<script lang="ts">
    import type { Snippet } from 'svelte';

    import { goto } from '$app/navigation';
    import { page } from '$app/stores';
    import * as Sidebar from '$comp/ui/sidebar';
    import { Toaster } from '$comp/ui/sonner';
    import { accessToken } from '$features/auth/index.svelte';
    import { type FetchClientContext, setAccessTokenFunc, setBaseUrl, setRequestOptions, useMiddleware } from '@exceptionless/fetchclient';
    import { error } from '@sveltejs/kit';
    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';
    import { ModeWatcher } from 'mode-watcher';
    import { get } from 'svelte/store';

    import '../app.css';
    import { routes } from './routes';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();

    setBaseUrl('api/v2');
    setRequestOptions({
        errorCallback: (response) => {
            throw response.problem ?? response;
        }
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

        if ((status === 0 || status === 503) && !ctx.options.expectedStatusCodes?.includes(status)) {
            const { url } = get(page);
            if (url.pathname.startsWith('/next/status')) {
                return;
            }

            await goto(`/next/status?redirect=${url.pathname}`, { replaceState: true });
        }
    });

    $effect(() => {
        const currentRoute = routes.find((route) => $page.url.pathname === route.href);
        if (currentRoute) {
            document.title = `${currentRoute.title} - Exceptionless`;
        } else {
            document.title = 'Exceptionless';
        }
    });

    const queryClient = new QueryClient({
        defaultOptions: {
            queries: {
                staleTime: 5 * 60 * 1000
            }
        }
    });
</script>

<div class="bg-background text-foreground">
    <ModeWatcher defaultMode={'dark'} />

    <QueryClientProvider client={queryClient}>
        <Sidebar.Provider>
            {@render children()}
        </Sidebar.Provider>

        <SvelteQueryDevtools />
    </QueryClientProvider>

    <Toaster position="bottom-right" />
</div>
