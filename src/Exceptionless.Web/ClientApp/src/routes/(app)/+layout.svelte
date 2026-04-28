<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { SavedView } from '$features/saved-views/models';
    import type { Snippet } from 'svelte';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useSidebar } from '$comp/ui/sidebar';
    import { env } from '$env/dynamic/public';
    import { getIntercomTokenQuery } from '$features/auth/api.svelte';
    import { accessToken, gotoLogin } from '$features/auth/index.svelte';
    import { UpgradeRequiredDialog } from '$features/billing';
    import { upgradeRequiredDialog } from '$features/billing/upgrade-required.svelte';
    import { invalidatePersistentEventQueries } from '$features/events/api.svelte';
    import { filterUsesPremiumFeatures } from '$features/events/premium-filter';
    import { buildIntercomBootOptions, IntercomShell } from '$features/intercom';
    import { shouldLoadIntercomOrganization } from '$features/intercom/config';
    import { getOrganizationQuery, getOrganizationsQuery, invalidateOrganizationQueries } from '$features/organizations/api.svelte';
    import OrganizationNotifications from '$features/organizations/components/organization-notifications.svelte';
    import { organization, showOrganizationNotifications } from '$features/organizations/context.svelte';
    import { invalidateProjectQueries } from '$features/projects/api.svelte';
    import { getSavedViewsQuery, invalidateSavedViewQueries } from '$features/saved-views/api.svelte';
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
    let requiresPremium = $derived(filterUsesPremiumFeatures(page.url.searchParams.get('filter')));
    const sidebar = useSidebar();
    let isCommandOpen = $state(false);

    function openCommandPalette(): void {
        isCommandOpen = true;
    }

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
                case 'SavedViewChanged':
                    await invalidateSavedViewQueries(queryClient, data.message);
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

    // Auth guard — re-check on navigation and token changes
    $effect(() => {
        const currentToken = accessToken.current;
        void page.url.pathname;

        if (!currentToken) {
            queryClient.cancelQueries();
            queryClient.invalidateQueries();
            gotoLogin();
        }
    });

    // WebSocket + keyboard shortcut — only depends on token, not navigation
    $effect(() => {
        const currentToken = accessToken.current;

        function handleKeydown(e: KeyboardEvent) {
            if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
                e.preventDefault();
                isCommandOpen = !isCommandOpen;
            }
        }

        if (!currentToken) {
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

    const intercomAppId = $derived(env.PUBLIC_INTERCOM_APPID ?? '');
    const intercomTokenQuery = getIntercomTokenQuery();
    const shouldFetchIntercomOrganization = $derived(shouldLoadIntercomOrganization(intercomAppId, intercomTokenQuery.isSuccess));

    // Query for current organization details (for Intercom company data)
    const currentOrganizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return shouldFetchIntercomOrganization ? organization.current : undefined;
            }
        }
    });
    const intercomOrganization = $derived(shouldFetchIntercomOrganization ? currentOrganizationQuery.data : undefined);

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

    const currentOrganization = $derived(organizations.find((organizationItem: ViewOrganization) => organizationItem.id === organization.current));
    const hasSavedViewsFeature = $derived(currentOrganization?.features?.includes('feature-saved-views') ?? false);

    const savedViewsQuery = getSavedViewsQuery({
        route: {
            get organizationId() {
                return hasSavedViewsFeature ? organization.current : undefined;
            }
        }
    });

    const viewToHref: Record<string, string> = {
        events: resolve('/(app)'),
        issues: resolve('/(app)/issues'),
        stream: resolve('/(app)/stream')
    };

    function buildSavedViewHref(baseHref: string, savedView: SavedView): string {
        const queryEntries: [string, string][] = [['saved', savedView.id]];
        if (savedView.filter) {
            queryEntries.push(['filter', savedView.filter]);
        }

        if (savedView.time) {
            queryEntries.push(['time', savedView.time]);
        }

        const queryParams = new URLSearchParams(queryEntries);
        return `${baseHref}?${queryParams.toString()}`;
    }

    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, impersonating: isImpersonating, user: meQuery.data };
        const allRoutes = routes().filter((route) => (route.show ? route.show(context) : true));

        const savedViews = savedViewsQuery.data ?? [];
        if (savedViews.length === 0) {
            return allRoutes;
        }

        return allRoutes.map((route) => {
            if (route.group !== 'Dashboards') {
                return route;
            }

            const viewKey = Object.entries(viewToHref).find(([, href]) => href === route.href)?.[0];
            if (!viewKey) {
                return route;
            }

            const viewSavedViews = savedViews.filter((savedView: SavedView) => savedView.view_type === viewKey);
            if (viewSavedViews.length === 0) {
                return route;
            }

            const defaultView = viewSavedViews.find((savedView: SavedView) => savedView.is_default);
            const nonDefaultViews = viewSavedViews.filter((savedView: SavedView) => !savedView.is_default);

            // Only show submenu if there are non-default views
            if (nonDefaultViews.length === 0) {
                return { ...route, defaultViewId: defaultView?.id, view: viewKey };
            }

            // Show all views sorted: default first, then alphabetically by name
            const sortedViews = [...viewSavedViews].sort((a, b) => {
                if (a.is_default && !b.is_default) {
                    return -1;
                }

                if (!a.is_default && b.is_default) {
                    return 1;
                }

                return a.name.localeCompare(b.name);
            });

            const children = sortedViews.map((savedView) => ({
                href: buildSavedViewHref(route.href, savedView),
                isDefault: savedView.is_default,
                title: savedView.name
            }));

            return {
                ...route,
                children,
                defaultViewId: defaultView?.id,
                view: viewKey
            };
        });
    });

    // Intercom configuration
    const intercomToken = $derived(intercomAppId ? intercomTokenQuery.data?.token : undefined);
    const intercomBootOptions = $derived(buildIntercomBootOptions(meQuery.data, intercomOrganization, intercomToken));
    let intercomUnreadCount = $state(0);
    const isChatEnabled = $derived(!!intercomAppId && !!intercomBootOptions);

    function onIntercomUnreadCountChange(unreadCount: number) {
        intercomUnreadCount = Math.max(0, unreadCount);
    }
</script>

{#snippet appShell(openChat: () => void)}
    <Navbar openCommand={openCommandPalette}></Navbar>
    <Sidebar routes={filteredRoutes}>
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
            <SidebarUser
                {isChatEnabled}
                isLoading={meQuery.isLoading}
                user={meQuery.data}
                {gravatar}
                isImpersonating={!!impersonatedOrganization}
                {organizations}
                {openChat}
                {intercomUnreadCount}
            />
        {/snippet}
    </Sidebar>
    <div class="flex min-h-screen min-w-0 flex-1 pt-16">
        <div class="text-secondary-foreground flex min-h-full min-w-0 flex-1 flex-col overflow-x-hidden overflow-y-auto">
            <main class="flex-1 px-4 pt-4">
                <NavigationCommand bind:open={isCommandOpen} routes={filteredRoutes} />

                {#if showOrganizationNotifications.current}
                    <OrganizationNotifications {isChatEnabled} {openChat} {requiresPremium} class="mb-4" />
                {/if}

                <div in:fade={{ delay: 150, duration: 150 }} out:fade={{ duration: 150 }}>
                    {@render children()}
                </div>
            </main>

            <Footer></Footer>
        </div>
    </div>
{/snippet}

{#if isAuthenticated}
    <IntercomShell
        appId={intercomAppId || undefined}
        bootOptions={intercomBootOptions}
        onUnreadCountChange={onIntercomUnreadCountChange}
        routeKey={page.url.pathname}
    >
        {#snippet children(openChat)}
            {@render appShell(openChat)}
        {/snippet}
    </IntercomShell>

    {#if upgradeRequiredDialog.open}
        <UpgradeRequiredDialog />
    {/if}
{/if}
