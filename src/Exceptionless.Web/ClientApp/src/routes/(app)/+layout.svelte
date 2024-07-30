<script lang="ts">
    import type { Snippet } from 'svelte';

    import { accessToken, gotoLogin } from '$api/auth.svelte';
    import { getMeQuery } from '$api/usersApi.svelte';
    import { WebSocketClient } from '$api/WebSocketClient.svelte';
    import { page } from '$app/stores';
    import { persisted } from '$lib/helpers/persisted.svelte';
    import { isEntityChangedType, type WebSocketMessageType } from '$lib/models/websocket';
    import { validate } from '$lib/validation/validation';
    import { setModelValidator, useMiddleware } from '@exceptionless/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { MediaQuery } from 'runed';

    import { type NavigationItemContext, routes } from '../routes';
    import FooterLayout from './(components)/layouts/Footer.svelte';
    import NavbarLayout from './(components)/layouts/Navbar.svelte';
    import SidebarLayout from './(components)/layouts/Sidebar.svelte';
    import NavigationCommand from './(components)/NavigationCommand.svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();
    let isAuthenticated = $derived(accessToken.value !== null);

    let isSidebarOpen = persisted('sidebar-open', false);
    let isCommandOpen = $state(false);
    const isSmallScreenQuery = new MediaQuery('(min-width: 640px)');
    const isMediumScreenQuery = new MediaQuery('(min-width: 768px)');
    const isLargeScreenQuery = new MediaQuery('(min-width: 1024px)');

    setModelValidator(validate);
    useMiddleware(async (ctx, next) => {
        await next();

        if (ctx.response && ctx.response.status === 401) {
            accessToken.value = null;
            return;
        }

        if (ctx.response?.headers.has('X-Result-Count') && ctx.response?.data !== null) {
            const resultCountHeaderValue = parseInt(ctx.response.headers.get('X-Result-Count') || '');

            ctx.response.meta.total = resultCountHeaderValue;

            if (typeof ctx.response?.data === 'object' && (ctx.response.data as { resultCount?: null | number }).resultCount === undefined) {
                (ctx.response.data as { resultCount?: null | number }).resultCount = !isNaN(resultCountHeaderValue) ? resultCountHeaderValue : null;
            }
        }
    });

    const queryClient = useQueryClient();
    async function onMessage(message: MessageEvent) {
        const data: { message: unknown; type: WebSocketMessageType } = message.data ? JSON.parse(message.data) : null;

        if (!data?.type) {
            return;
        }

        document.dispatchEvent(
            new CustomEvent(data.type, {
                bubbles: true,
                detail: data.message
            })
        );

        if (isEntityChangedType(data)) {
            await queryClient.invalidateQueries({ queryKey: [data.message.type] });
        }

        // This event is fired when a user is added or removed from an organization.
        // if (data.type === "UserMembershipChanged" && data.message?.organization_id) {
        //     $rootScope.$emit("OrganizationChanged", data.message);
        //     $rootScope.$emit("ProjectChanged", data.message);
        // }
    }

    // Close Sidebar on page change on mobile
    page.subscribe(() => {
        if (isSmallScreenQuery.matches) {
            isSidebarOpen.value = false;
        }
    });

    $effect(() => {
        function handleKeydown(e: KeyboardEvent) {
            if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
                e.preventDefault();
                isCommandOpen = !isCommandOpen;
            }
        }

        if (!isAuthenticated) {
            gotoLogin();
            return;
        }

        document.addEventListener('keydown', handleKeydown);

        const ws = new WebSocketClient();
        ws.onMessage = onMessage;
        ws.onOpen = (_, isReconnect) => {
            if (isReconnect) {
                document.dispatchEvent(
                    new CustomEvent('refresh', {
                        bubbles: true,
                        detail: 'WebSocket Connected'
                    })
                );
            }
        };

        return () => {
            document.removeEventListener('keydown', handleKeydown);
            ws?.close();
        };
    });

    const userQuery = getMeQuery();
    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, user: userQuery.data };
        return routes.filter((route) => (route.show ? route.show(context) : true));
    });
</script>

{#if isAuthenticated}
    <NavbarLayout bind:isCommandOpen bind:isSidebarOpen={isSidebarOpen.value} isMediumScreen={isMediumScreenQuery.matches}></NavbarLayout>
    <div class="flex overflow-hidden pt-16">
        <SidebarLayout bind:isSidebarOpen={isSidebarOpen.value} isLargeScreen={isLargeScreenQuery.matches} routes={filteredRoutes} />

        <div class="relative h-full w-full overflow-y-auto text-secondary-foreground {isSidebarOpen.value ? 'lg:ml-64' : 'lg:ml-16'}">
            <main>
                <div class="px-4 pt-4">
                    <NavigationCommand bind:open={isCommandOpen} routes={filteredRoutes} />
                    {@render children()}
                </div>
            </main>

            <FooterLayout></FooterLayout>
        </div>
    </div>
{/if}
