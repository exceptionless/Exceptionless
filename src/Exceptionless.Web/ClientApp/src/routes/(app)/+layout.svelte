<script lang="ts">
    import type { Snippet } from 'svelte';

    import { page } from '$app/state';
    import { useSidebar } from '$comp/ui/sidebar';
    import { accessToken, gotoLogin } from '$features/auth/index.svelte';
    import { invalidatePersistentEventQueries } from '$features/events/api.svelte';
    import { getOrganizationQuery, invalidateOrganizationQueries } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { invalidateProjectQueries } from '$features/projects/api.svelte';
    import { invalidateStackQueries } from '$features/stacks/api.svelte';
    import { invalidateTokenQueries } from '$features/tokens/api.svelte';
    import { getMeQuery, invalidateUserQueries } from '$features/users/api.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { isEntityChangedType, type WebSocketMessageType } from '$features/websockets/models';
    import { WebSocketClient } from '$features/websockets/web-socket-client.svelte';
    import { validate } from '$shared/validation';
    import { setModelValidator, useMiddleware } from '@exceptionless/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { fade } from 'svelte/transition';

    import { type NavigationItemContext, routes } from '../routes.svelte';
    import Footer from './(components)/layouts/footer.svelte';
    import Navbar from './(components)/layouts/navbar.svelte';
    import SidebarOrganizationSwitcher from './(components)/layouts/sidebar-organization-switcher.svelte';
    import SidebarUser from './(components)/layouts/sidebar-user.svelte';
    import Sidebar from './(components)/layouts/sidebar.svelte';
    import NavigationCommand from './(components)/navigation-command.svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();
    let isAuthenticated = $derived(accessToken.current !== null);
    const sidebar = useSidebar();
    let isCommandOpen = $state(false);

    setModelValidator(validate);
    useMiddleware(async (ctx, next) => {
        await next();

        if (ctx.response && ctx.response.status === 401) {
            accessToken.current = null;
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
            switch (data.type) {
                case 'OrganizationChanged':
                    await invalidateOrganizationQueries(queryClient, data.message);
                    break;
                case 'PersistentEventChanged':
                    await invalidatePersistentEventQueries(queryClient, data.message);
                    break;
                case 'ProjectChanged':
                    await invalidateProjectQueries(queryClient, data.message);
                    break;
                case 'StackChanged':
                    await invalidateStackQueries(queryClient, data.message);
                    break;
                case 'TokenChanged':
                    await invalidateTokenQueries(queryClient, data.message);
                    break;
                case 'UserChanged':
                    await invalidateUserQueries(queryClient, data.message);
                    break;
                default:
                    await queryClient.invalidateQueries({ queryKey: [data.message.type] });
                    break;
            }
        }

        // This event is fired when a user is added or removed from an organization.
        // if (data.type === "UserMembershipChanged" && data.message?.organization_id) {
        //     $rootScope.$emit("OrganizationChanged", data.message);
        //     $rootScope.$emit("ProjectChanged", data.message);
        // }
    }

    // Close Sidebar on page change on mobile
    let lastPage = $state(page.url.pathname);
    $effect(() => {
        if (lastPage === page.url.pathname) {
            return;
        }

        lastPage = page.url.pathname;
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
                queryClient.invalidateQueries();
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

    const userResponse = getMeQuery();
    const gravatar = getGravatarFromCurrentUser(userResponse);

    const organizationsResponse = getOrganizationQuery({});
    $effect(() => {
        if (!organizationsResponse.isSuccess) {
            return;
        }

        if (organizationsResponse.data.length === 0) {
            // TODO: Redirect to create organization page.
            organization.current = undefined;
            return;
        }

        if (!organizationsResponse.data.find((org) => org.id === organization.current)) {
            organization.current = organizationsResponse.data[0]!.id;
        }
    });

    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, user: userResponse.data };
        return routes().filter((route) => (route.show ? route.show(context) : true));
    });
</script>

{#if isAuthenticated}
    <Navbar bind:isCommandOpen></Navbar>
    <Sidebar routes={filteredRoutes}>
        {#snippet header()}
            <SidebarOrganizationSwitcher
                class="pt-2"
                isLoading={organizationsResponse.isLoading}
                organizations={organizationsResponse.data}
                bind:selected={organization.current}
            />
        {/snippet}

        {#snippet footer()}
            <SidebarUser isLoading={userResponse.isLoading} user={userResponse.data} {gravatar} />
        {/snippet}
    </Sidebar>
    <div class="flex w-full overflow-hidden pt-16">
        <div class="text-secondary-foreground w-full">
            <main class="px-4 pt-4">
                <NavigationCommand bind:open={isCommandOpen} routes={filteredRoutes} />
                {#key page.url.pathname}
                    <div in:fade={{ delay: 150, duration: 150 }} out:fade={{ duration: 150 }}>
                        {@render children()}
                    </div>
                {/key}
            </main>

            <Footer></Footer>
        </div>
    </div>
{/if}
