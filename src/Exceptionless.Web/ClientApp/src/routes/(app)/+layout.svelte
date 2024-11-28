<script lang="ts">
    import type { Snippet } from 'svelte';

    import { page } from '$app/stores';
    import { useSidebar } from '$comp/ui/sidebar';
    import { accessToken, gotoLogin } from '$features/auth/index.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { isEntityChangedType, type WebSocketMessageType } from '$features/websockets/models';
    import { WebSocketClient } from '$features/websockets/WebSocketClient.svelte';
    import { validate } from '$shared/validation';
    import { setModelValidator, useMiddleware } from '@exceptionless/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { fade } from 'svelte/transition';

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
    const sidebar = useSidebar();
    let isCommandOpen = $state(false);

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
        if (sidebar.isMobile) {
            sidebar.setOpen(false);
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
            queryClient.cancelQueries();
            queryClient.invalidateQueries();

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
    <NavbarLayout bind:isCommandOpen></NavbarLayout>
    <SidebarLayout routes={filteredRoutes} />
    <div class="flex w-full overflow-hidden pt-16">
        <div class="w-full text-secondary-foreground">
            <main class="px-4 pt-4">
                <NavigationCommand bind:open={isCommandOpen} routes={filteredRoutes} />
                {#key $page.url.pathname}
                    <div in:fade={{ delay: 150, duration: 150 }} out:fade={{ duration: 150 }}>
                        {@render children()}
                    </div>
                {/key}
            </main>

            <FooterLayout></FooterLayout>
        </div>
    </div>
{/if}
