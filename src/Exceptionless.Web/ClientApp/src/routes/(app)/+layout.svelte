<script lang="ts">
    import type { SavedView } from '$features/saved-views/models';
    import type { Snippet } from 'svelte';

    import { beforeNavigate, goto } from '$app/navigation';
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
    import { premiumPage } from '$features/organizations/premium-page.svelte';
    import { invalidateProjectQueries } from '$features/projects/api.svelte';
    import { getSavedViewsQuery, invalidateSavedViewQueries, isSavedViewDeleted } from '$features/saved-views/api.svelte';
    import { savedViewHref } from '$features/saved-views/slugs';
    import { appKeyboardShortcuts, isKeyboardShortcut } from '$features/shared/keyboard-shortcuts';
    import { invalidateStackQueries } from '$features/stacks/api.svelte';
    import { invalidateTokenQueries } from '$features/tokens/api.svelte';
    import { getMeQuery, invalidateUserQueries } from '$features/users/api.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { invalidateWebhookQueries } from '$features/webhooks/api.svelte';
    import { isEntityChangedType, type WebSocketMessageType } from '$features/websockets/models';
    import { SseClient } from '$features/websockets/sse-client.svelte';
    import { useMiddleware } from '@exceptionless/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { tick } from 'svelte';
    import { fade } from 'svelte/transition';

    import { type NavigationItemContext, routes } from '../routes.svelte';
    import KeyboardShortcutsDialog from './(components)/keyboard-shortcuts-dialog.svelte';
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
    let requiresPremium = $derived(premiumPage.requiresPremium || filterUsesPremiumFeatures(page.url.searchParams.get('filter')));
    const sidebar = useSidebar();
    let isCommandOpen = $state(false);
    let commandResetKey = $state(0);
    let isKeyboardShortcutsOpen = $state(false);
    let isOrganizationSwitcherOpen = $state(false);
    let isUserMenuOpen = $state(false);

    // Auto-reset premium page state on navigation so pages don't need cleanup
    beforeNavigate(() => {
        premiumPage.current = undefined;
    });

    function openCommandPalette(): void {
        commandResetKey += 1;
        isCommandOpen = true;
    }

    async function openOrganizationSwitcher(): Promise<void> {
        isCommandOpen = false;
        isKeyboardShortcutsOpen = false;
        isUserMenuOpen = false;
        await tick();
        isOrganizationSwitcherOpen = true;
    }

    async function openUserMenu(): Promise<void> {
        isCommandOpen = false;
        isKeyboardShortcutsOpen = false;
        isOrganizationSwitcherOpen = false;
        await tick();
        isUserMenuOpen = true;
    }

    async function openKeyboardShortcuts(): Promise<void> {
        isCommandOpen = false;
        isOrganizationSwitcherOpen = false;
        isUserMenuOpen = false;
        await tick();
        isKeyboardShortcutsOpen = true;
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

        // When a user is added or removed from an organization, invalidate org/project caches
        // so the UI reflects the membership change without a manual reload.
        if (data.type === 'UserMembershipChanged') {
            const msg = data.message as { organization_id?: string };
            if (msg?.organization_id) {
                await invalidateOrganizationQueries(queryClient, msg);
                await invalidateProjectQueries(queryClient, msg);
            }
        }
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

    // SSE + keyboard shortcuts — only depends on token, not navigation
    $effect(() => {
        const currentToken = accessToken.current;

        function handleKeydown(e: KeyboardEvent) {
            if (
                e.defaultPrevented ||
                e.ctrlKey ||
                e.metaKey ||
                e.altKey ||
                isCommandOpen ||
                isKeyboardShortcutsOpen ||
                isOrganizationSwitcherOpen ||
                isUserMenuOpen ||
                isEditableElement(e.target)
            ) {
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.commandPalette)) {
                e.preventDefault();
                openCommandPalette();
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.switchOrganization)) {
                e.preventDefault();
                void openOrganizationSwitcher();
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.userMenu)) {
                e.preventDefault();
                void openUserMenu();
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.allEvents)) {
                e.preventDefault();
                void goto(resolve('/(app)/events'));
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.stacks)) {
                e.preventDefault();
                void goto(resolve('/(app)/stacks'));
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.keyboardShortcuts)) {
                e.preventDefault();
                void openKeyboardShortcuts();
            }
        }

        if (!currentToken) {
            return;
        }

        document.addEventListener('keydown', handleKeydown, { capture: true });

        const sse = new SseClient();
        sse.onMessage = onMessage;
        sse.onOpen = (isReconnect) => {
            if (isReconnect) {
                queryClient.invalidateQueries();
                document.dispatchEvent(
                    new CustomEvent('refresh', {
                        bubbles: true,
                        detail: 'SSE Connected'
                    })
                );
            }
        };

        return () => {
            document.removeEventListener('keydown', handleKeydown, { capture: true });
            sse?.close();
        };
    });

    function isEditableElement(target: EventTarget | null): boolean {
        if (!(target instanceof HTMLElement)) {
            return false;
        }

        return target.isContentEditable || ['INPUT', 'SELECT', 'TEXTAREA'].includes(target.tagName);
    }

    const meQuery = getMeQuery();
    const gravatar = getGravatarFromCurrentUser(meQuery);
    const isGlobalAdmin = $derived(!!meQuery.data?.roles?.includes('global'));

    const organizationsQuery = getOrganizationsQuery({});
    const organizations = $derived(organizationsQuery.data?.data ?? []);

    const impersonatingOrganizationId = $derived.by(() => {
        // Only consider impersonation if user data is loaded and user has organizations
        const userOrganizationIds = meQuery.data?.organization_ids;
        if (!userOrganizationIds || userOrganizationIds.length === 0 || !organization.current) {
            return undefined;
        }

        const isUserOrganization = userOrganizationIds.includes(organization.current);
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

    // Keep selected organization synchronized with current memberships.
    $effect(() => {
        void page.url.pathname;

        if (!organizationsQuery.isSuccess) {
            return;
        }

        const hasOrganizations = organizations.length > 0;
        if (!hasOrganizations) {
            organization.current = undefined;

            // Redirect non-admins to add organization page
            if (!isGlobalAdmin && !organizationsQuery.isLoading) {
                const addOrganizationPath = resolve('/(app)/organization/add');
                if (page.url.pathname !== addOrganizationPath) {
                    goto(addOrganizationPath);
                }
            }

            return;
        }

        const hasSelectedOrganization = !!organization.current && organizations.some((organizationItem) => organizationItem.id === organization.current);
        if (!hasSelectedOrganization && !impersonatingOrganizationId) {
            organization.current = organizations[0]!.id;
        }
    });

    const isImpersonating = $derived(!!impersonatedOrganization);

    const savedViewsQuery = getSavedViewsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const viewToHref: Record<string, string> = {
        events: resolve('/(app)/events'),
        stacks: resolve('/(app)/stacks'),
        stream: resolve('/(app)/stream')
    };

    function buildSavedViewHref(savedView: SavedView): string {
        return savedViewHref(savedView);
    }

    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, impersonating: isImpersonating, user: meQuery.data };
        const allRoutes = routes().filter((route) => (route.show ? route.show(context) : true));

        const savedViews = (savedViewsQuery.data ?? []).filter((savedView) => !isSavedViewDeleted(savedView));
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

            const sortedViews = [...viewSavedViews].sort((a, b) => a.name.localeCompare(b.name));

            const children = [
                ...sortedViews.map((savedView) => ({
                    href: buildSavedViewHref(savedView),
                    title: savedView.name
                })),
                ...(route.children ?? [])
            ];

            return {
                ...route,
                children,
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

    const setupPath = resolve('/(app)/organization/add');
    const isSetupPage = $derived(page.url.pathname === setupPath);
</script>

{#snippet setupShell()}
    <div class="flex h-screen w-full items-center justify-center px-4">
        <main class="w-full">
            <div in:fade={{ delay: 150, duration: 150 }} out:fade={{ duration: 150 }}>
                {@render children()}
            </div>
        </main>
    </div>
{/snippet}

{#snippet appShell(openChat: () => void)}
    <Navbar openCommand={openCommandPalette}></Navbar>
    <Sidebar routes={filteredRoutes}>
        {#snippet header()}
            <SidebarOrganizationSwitcher
                isLoading={organizationsQuery.isLoading}
                {organizations}
                {impersonatedOrganization}
                bind:open={isOrganizationSwitcherOpen}
                bind:currentOrganizationId={organization.current}
            />
        {/snippet}

        {#snippet footer()}
            <SidebarUser
                {isChatEnabled}
                isLoading={meQuery.isLoading}
                user={meQuery.data}
                {gravatar}
                {organizations}
                {openChat}
                {openKeyboardShortcuts}
                {intercomUnreadCount}
                bind:open={isUserMenuOpen}
            />
        {/snippet}
    </Sidebar>
    <div class="flex h-screen min-w-0 flex-1 flex-col overflow-hidden pt-16">
        <div class="text-secondary-foreground flex min-h-0 min-w-0 flex-1 scrollbar-gutter-stable flex-col overflow-x-hidden overflow-y-auto">
            <main class="flex-1 px-4 pt-4">
                <NavigationCommand
                    bind:open={isCommandOpen}
                    {openKeyboardShortcuts}
                    {openOrganizationSwitcher}
                    {openUserMenu}
                    resetKey={commandResetKey}
                    routes={filteredRoutes}
                />
                <KeyboardShortcutsDialog bind:open={isKeyboardShortcutsOpen} />

                {#if showOrganizationNotifications.current}
                    <OrganizationNotifications {isChatEnabled} {openChat} {requiresPremium} premiumFeatureName={premiumPage.current} class="mb-4" />
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
            {#if isSetupPage}
                {@render setupShell()}
            {:else}
                {@render appShell(openChat)}
            {/if}
        {/snippet}
    </IntercomShell>

    {#if upgradeRequiredDialog.open}
        <UpgradeRequiredDialog />
    {/if}
{/if}
