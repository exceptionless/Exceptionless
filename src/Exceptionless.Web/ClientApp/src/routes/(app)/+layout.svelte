<script lang="ts">
    import type { Snippet } from 'svelte';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useSidebar } from '$comp/ui/sidebar';
    import { env } from '$env/dynamic/public';
    import { accessToken, gotoLogin } from '$features/auth/index.svelte';
    import { invalidatePersistentEventQueries } from '$features/events/api.svelte';
    import { getOrganizationQuery, getOrganizationsQuery, invalidateOrganizationQueries } from '$features/organizations/api.svelte';
    import OrganizationNotifications from '$features/organizations/components/organization-notifications.svelte';
    import { organization, showOrganizationNotifications } from '$features/organizations/context.svelte';
    import { invalidateProjectQueries } from '$features/projects/api.svelte';
    import { invalidateStackQueries } from '$features/stacks/api.svelte';
    import { invalidateTokenQueries } from '$features/tokens/api.svelte';
    import { getMeQuery, invalidateUserQueries } from '$features/users/api.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { invalidateWebhookQueries } from '$features/webhooks/api.svelte';
    import { isEntityChangedType, type WebSocketMessageType } from '$features/websockets/models';
    import { WebSocketClient } from '$features/websockets/web-socket-client.svelte';
    import { useMiddleware } from '@exceptionless/fetchclient';
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
    let isAuthenticated = $derived(!!accessToken.current);
    const sidebar = useSidebar();
    let isCommandOpen = $state(false);

    useMiddleware(async (ctx, next) => {
        await next();

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
                case 'WebHookChanged':
                    await invalidateWebhookQueries(queryClient, data.message);
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
        // Direct read of accessToken.current establishes reactive dependency, working around PersistedState reactivity bug
        const currentToken = accessToken.current;
        // Track page.url to ensure effect re-runs on navigation
        void page.url.pathname;

        function handleKeydown(e: KeyboardEvent) {
            if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
                e.preventDefault();
                isCommandOpen = !isCommandOpen;
            }
        }

        // Check token directly instead of using derived isAuthenticated
        if (!currentToken) {
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

    const meQuery = getMeQuery();
    const gravatar = getGravatarFromCurrentUser(meQuery);
    const isGlobalAdmin = $derived(!!meQuery.data?.roles?.includes('global'));

    const organizationsQuery = getOrganizationsQuery({});
    const organizations = $derived(organizationsQuery.data?.data ?? []);

    const impersonatingOrganizationId = $derived.by(() => {
        const isUserOrganization = meQuery.data?.organization_ids.includes(organization.current ?? '');
        return isUserOrganization ? undefined : organization.current;
    });

    const impersonatedOrganizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return impersonatingOrganizationId;
            }
        }
    });
    const impersonatedOrganization = $derived(impersonatingOrganizationId ? impersonatedOrganizationQuery.data : undefined);

    // Simple organization selection - pick first available if none selected
    $effect(() => {
        if (!organizationsQuery.isSuccess) {
            return;
        }

        const hasOrganizations = organizations.length > 0;
        if (!hasOrganizations) {
            organization.current = undefined;

            // Redirect non-admins to add organization page
            if (!isGlobalAdmin && !organizationsQuery.isLoading) {
                goto(resolve(`/(app)/organization/add`));
            }

            return;
        }

        // Select first organization if none selected
        if (!organization.current) {
            organization.current = organizations[0]!.id;
        }
    });

    const isImpersonating = $derived(!!impersonatedOrganization);

    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, impersonating: isImpersonating, user: meQuery.data };
        return routes().filter((route) => (route.show ? route.show(context) : true));
    });

    const isChatEnabled = $derived(!!env.PUBLIC_INTERCOM_APPID);
    function openChat() {
        // TODO: Implement chat opening logic
    }
</script>

{#if isAuthenticated}
    <Navbar bind:isCommandOpen></Navbar>
    <Sidebar routes={filteredRoutes} impersonating={!!impersonatedOrganization}>
        {#snippet header()}
            <SidebarOrganizationSwitcher
                class="pt-2"
                isLoading={organizationsQuery.isLoading}
                {organizations}
                {impersonatedOrganization}
                bind:currentOrganizationId={organization.current}
            />
        {/snippet}

        {#snippet footer()}
            <SidebarUser isLoading={meQuery.isLoading} user={meQuery.data} {gravatar} isImpersonating={!!impersonatedOrganization} {organizations} />
        {/snippet}
    </Sidebar>
    <div class="flex w-full pt-16">
        <div class="text-secondary-foreground w-full overflow-y-auto">
            <main class="px-4 pt-4">
                <NavigationCommand bind:open={isCommandOpen} routes={filteredRoutes} />

                {#if showOrganizationNotifications.current}
                    <OrganizationNotifications {isChatEnabled} {openChat} class="mb-4" />
                {/if}

                <div in:fade={{ delay: 150, duration: 150 }} out:fade={{ duration: 150 }}>
                    {@render children()}
                </div>
            </main>

            <Footer></Footer>
        </div>
    </div>
{/if}
