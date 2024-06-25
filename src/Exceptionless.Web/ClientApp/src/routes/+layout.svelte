<script lang="ts">
    import type { Snippet } from 'svelte';
    import { setAccessTokenFunc, setBaseUrl, useMiddleware, type FetchClientContext } from '@exceptionless/fetchclient';
    import { error } from '@sveltejs/kit';

    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query-runes';
    import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';
    import { ModeWatcher } from 'mode-watcher';

    import { page } from '$app/stores';
    import { accessToken } from '$api/auth.svelte';
    import { Toaster } from '$comp/ui/sonner';

    import '../app.css';
    import { routes } from './routes';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();

    setBaseUrl('api/v2');
    setAccessTokenFunc(() => accessToken.value);

    useMiddleware(async (ctx: FetchClientContext, next: () => Promise<void>) => {
        await next();

        if (ctx.response?.status === 404 && !ctx.options.expectedStatusCodes?.includes(404)) {
            throw error(404, 'Not found');
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
        {@render children()}

        <SvelteQueryDevtools />
    </QueryClientProvider>

    <Toaster position="bottom-right" />
</div>
