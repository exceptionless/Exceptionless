<script lang="ts">
    import { setAccessTokenFunc, setBaseUrl, useMiddleware, type FetchClientContext, type Next } from '@exceptionless/fetchclient';
    import { error } from '@sveltejs/kit';

    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';
    import { ModeWatcher } from 'mode-watcher';

    import { page } from '$app/stores';
    import { accessToken } from '$api/auth.svelte';
    import { Toaster } from '$comp/ui/sonner';

    import '../app.css';
    import { routes } from './routes';

    setBaseUrl('api/v2');
    setAccessTokenFunc(() => accessToken.value);

    useMiddleware(async (ctx: FetchClientContext, next: Next) => {
        await next();

        if (ctx.response?.status === 404 && !ctx.options.expectedStatusCodes?.includes(404)) {
            throw error(404, 'Not found');
        }
    });

    $effect(() => {
        // eslint-disable-next-line svelte/valid-compile
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
        <slot />

        <SvelteQueryDevtools />
    </QueryClientProvider>

    <Toaster position="bottom-right" />
</div>
