<script lang="ts">
    import type { Snippet } from 'svelte';
    import { page } from '$app/stores';

    import NavbarLayout from './(components)/layouts/Navbar.svelte';
    import SidebarLayout from './(components)/layouts/Sidebar.svelte';
    import FooterLayout from './(components)/layouts/Footer.svelte';

    import { accessToken, gotoLogin } from '$api/auth.svelte';
    import { WebSocketClient } from '$api/WebSocketClient.svelte';
    import { isEntityChangedType, type WebSocketMessageType } from '$lib/models/websocket';
    import { setModelValidator, useMiddleware } from '@exceptionless/fetchclient';
    import { validate } from '$lib/validation/validation';

    import { useQueryClient } from '@tanstack/svelte-query-runes';
    import NavigationCommand from './(components)/NavigationCommand.svelte';
    import { getMeQuery } from '$api/usersApi.svelte';
    import { routes, type NavigationItemContext } from '../routes';
    import { persisted } from '$lib/helpers/persisted.svelte';
    import { mediaQuery } from '$lib/helpers/mediaQuery.svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();
    let isAuthenticated = $derived(accessToken.value !== null);

    let isSidebarOpen = persisted('sidebar-open', false);
    let isCommandOpen = $state(false);
    const isSmallScreen = mediaQuery('(min-width: 640px)');
    const isMediumScreen = mediaQuery('(min-width: 768px)');
    const isLargeScreen = mediaQuery('(min-width: 1024px)');

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

            if (typeof ctx.response?.data === 'object' && (ctx.response.data as { resultCount?: number | null }).resultCount === undefined) {
                (ctx.response.data as { resultCount?: number | null }).resultCount = !isNaN(resultCountHeaderValue) ? resultCountHeaderValue : null;
            }
        }
    });

    const queryClient = useQueryClient();
    async function onMessage(message: MessageEvent) {
        const data: { type: WebSocketMessageType; message: unknown } = message.data ? JSON.parse(message.data) : null;

        if (!data?.type) {
            return;
        }

        document.dispatchEvent(
            new CustomEvent(data.type, {
                detail: data.message,
                bubbles: true
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
        if (isSmallScreen === true) {
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
                        detail: 'WebSocket Connected',
                        bubbles: true
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
    <NavbarLayout bind:isCommandOpen bind:isSidebarOpen={isSidebarOpen.value} {isMediumScreen}></NavbarLayout>
    <div class="flex overflow-hidden pt-16">
        <SidebarLayout bind:isSidebarOpen={isSidebarOpen.value} {isLargeScreen} routes={filteredRoutes} />

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
