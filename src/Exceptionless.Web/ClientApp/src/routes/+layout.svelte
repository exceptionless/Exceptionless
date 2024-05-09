<script lang="ts">
    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';
    import { ModeWatcher } from 'mode-watcher';

    import { page } from '$app/stores';
    import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
    import { accessToken } from '$api/auth';
    import { Toaster } from '$comp/ui/sonner';

    import '../app.css';
    import { routes } from './routes';

    setDefaultBaseUrl('api/v2');
    setAccessTokenStore(accessToken);

    page.subscribe(($page) => {
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
