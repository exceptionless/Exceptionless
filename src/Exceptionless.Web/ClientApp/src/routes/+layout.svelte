<script lang="ts">
    import { onMount } from 'svelte';
    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { SvelteQueryDevtools } from '@tanstack/svelte-query-devtools';
    import { ModeWatcher } from 'mode-watcher';

    import { page } from '$app/stores';
    import { setDefaultBaseUrl, setAccessTokenStore } from '$api/FetchClient';
    import { accessToken, isAuthenticated } from '$api/auth';
    import { Toaster } from '$comp/ui/sonner';
    import NavigationCommand from './(components)/NavigationCommand.svelte';

    import '../app.css';
    import { routes, type NavigationItemContext, type NavigationItem } from './routes';
    import { getMeQuery } from '$api/queries/users';
    import { derived } from 'svelte/store';
    import { isCommandOpen } from '$lib/stores/app';

    onMount(() => {
        function handleKeydown(e: KeyboardEvent) {
            if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
                e.preventDefault();
                $isCommandOpen = !$isCommandOpen;
            }
        }
        document.addEventListener('keydown', handleKeydown);
        return () => {
            document.removeEventListener('keydown', handleKeydown);
        };
    });

    setDefaultBaseUrl('api/v2');
    setAccessTokenStore(accessToken);

    const queryClient = new QueryClient();
    const userQuery = getMeQuery(queryClient);
    const filteredRoutes = derived(userQuery, ($userResponse) => {
        const context: NavigationItemContext = { authenticated: $isAuthenticated, user: $userResponse.data };
        return routes.filter((route) => (route.show ? route.show(context) : true));
    });
</script>

<div class="bg-background text-foreground">
    <ModeWatcher defaultMode={'dark'} />

    <QueryClientProvider client={queryClient}>
        <NavigationCommand bind:open={$isCommandOpen} routes={$filteredRoutes} />
        <slot />

        <SvelteQueryDevtools />
    </QueryClientProvider>

    <Toaster position="bottom-right" />
</div>
