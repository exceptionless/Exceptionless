<script lang="ts">
    import type { AssistantPromptRequest } from '$features/assistant/models';
    import type { SavedView } from '$features/saved-views/models';
    import type { Snippet } from 'svelte';

    import { beforeNavigate, goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useSidebar } from '$comp/ui/sidebar';
    import { env } from '$env/dynamic/public';
    import { resolveAssistantAccessState } from '$features/assistant/access-state';
    import { getAssistantAccessQuery, invalidateAssistantAccessQueries } from '$features/assistant/api.svelte';
    import { setAssistantControls } from '$features/assistant/controls.svelte';
    import { assistantPageContext, type AssistantResourceContext } from '$features/assistant/page-context.svelte';
    import { getIntercomTokenQuery } from '$features/auth/api.svelte';
    import { accessToken, gotoLogin } from '$features/auth/index.svelte';
    import { ChangePlanDialogHost, UpgradeRequiredDialog } from '$features/billing';
    import {
        createOrganizationEventNotificationRefresher,
        invalidatePersistentEventQueries,
        type OrganizationEventNotificationRefresher
    } from '$features/events/api.svelte';
    import { filterUsesPremiumFeatures, getSearchResourceForPathname } from '$features/events/premium-filter';
    import { buildIntercomBootOptions, IntercomShell } from '$features/intercom';
    import { shouldLoadIntercomOrganization } from '$features/intercom/config';
    import { getIntercomRouteKey } from '$features/intercom/updates';
    import Notifications from '$features/notifications/components/notifications.svelte';
    import {
        getOrganizationQuery,
        getOrganizationsQuery,
        invalidateOrganizationQueries,
        invalidateOrganizationUsageQueries,
        invalidatePlanOverageQueries
    } from '$features/organizations/api.svelte';
    import OrganizationNotifications from '$features/organizations/components/organization-notifications.svelte';
    import { organization, showOrganizationNotifications } from '$features/organizations/context.svelte';
    import { premiumPage } from '$features/organizations/premium-page.svelte';
    import { getUtcMonthKey, ORGANIZATION_USAGE_ROLLOVER_CHECK_INTERVAL_MS } from '$features/organizations/utils';
    import { invalidateProjectQueries } from '$features/projects/api.svelte';
    import { getSavedViewsQuery, invalidateSavedViewQueries, isSavedViewDeleted } from '$features/saved-views/api.svelte';
    import { savedViewHref } from '$features/saved-views/slugs';
    import { appKeyboardShortcuts, isKeyboardShortcut } from '$features/shared/keyboard-shortcuts';
    import { createProjectStackNotificationRefresher, invalidateStackQueries, type ProjectStackNotificationRefresher } from '$features/stacks/api.svelte';
    import { invalidateTokenQueries } from '$features/tokens/api.svelte';
    import { getMeQuery, invalidateUserQueries } from '$features/users/api.svelte';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import { invalidateWebhookQueries } from '$features/webhooks/api.svelte';
    import {
        ChangeType,
        type EntityChanged,
        isEntityChangedType,
        isPlanOverageType,
        type UserMembershipChanged,
        type WebSocketMessageType
    } from '$features/websockets/models';
    import { SseClient } from '$features/websockets/sse-client.svelte';
    import { Telemetry } from '$lib/telemetry';
    import { useMiddleware } from '@foundatiofx/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { useInterval } from 'runed';
    import { tick } from 'svelte';
    import { SvelteURLSearchParams } from 'svelte/reactivity';
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
    const assistantPageHref = resolve('/(app)/exie');
    let isAuthenticated = $derived(!!accessToken.current);
    let requiresPremium = $derived(
        premiumPage.requiresPremium || filterUsesPremiumFeatures(page.url.searchParams.get('filter'), getSearchResourceForPathname(page.url.pathname))
    );
    const sidebar = useSidebar();
    let isCommandOpen = $state(false);
    let isAssistantOpen = $state(false);
    let AssistantPanel = $state<typeof import('$features/assistant/components/assistant-panel.svelte').default>();
    let assistantPromptRequest = $state<AssistantPromptRequest>();
    let isAssistantPage = $derived(page.url.pathname === assistantPageHref);
    let assistantResourceContext = $derived(isAssistantPage ? undefined : assistantPageContext.getContext(page.params.eventId, page.params.stackId));
    let assistantProjectId = $derived(
        isAssistantPage ? (page.url.searchParams.get('project') ?? undefined) : (assistantResourceContext?.projectId ?? page.params.projectId)
    );
    let assistantPath = $derived(getAssistantPath(assistantResourceContext, getAssistantSourcePath()));
    let assistantExpandHref = $derived(buildAssistantPageHref(`${page.url.pathname}${page.url.search}`, assistantPath, assistantProjectId));
    let assistantReturnHref = $derived(getAssistantReturnHref());
    let commandResetKey = $state(0);
    let isKeyboardShortcutsOpen = $state(false);
    let isOrganizationSwitcherOpen = $state(false);
    let isImpersonateOrganizationOpen = $state(false);
    let isUserMenuOpen = $state(false);

    // Auto-reset premium page state on navigation so pages don't need cleanup
    beforeNavigate(() => {
        premiumPage.current = undefined;
    });

    function openCommandPalette(): void {
        commandResetKey += 1;
        isCommandOpen = true;
    }

    async function toggleAssistantPanel(): Promise<void> {
        if (isAssistantPage) {
            await goto(assistantReturnHref);
            return;
        }

        if (!isAssistantOpen) {
            await loadAssistantPanel();
        }

        isAssistantOpen = !isAssistantOpen;
    }

    async function openAssistantPanel(): Promise<void> {
        await loadAssistantPanel();
        isAssistantOpen = true;
    }

    async function askAssistant(prompt: string): Promise<void> {
        await loadAssistantPanel();
        isAssistantOpen = true;
        assistantPromptRequest = {
            id: crypto.randomUUID(),
            prompt
        };
    }

    async function loadAssistantPanel(): Promise<void> {
        AssistantPanel ??= (await import('$features/assistant/components/assistant-panel.svelte')).default;
    }

    function buildAssistantPageHref(returnHref: string, contextPath: string, projectId: string | undefined): string {
        const queryParameters = new SvelteURLSearchParams();
        queryParameters.set('from', returnHref);
        if (contextPath !== returnHref) {
            queryParameters.set('context', contextPath);
        }

        if (projectId) {
            queryParameters.set('project', projectId);
        }

        return `${assistantPageHref}?${queryParameters}`;
    }

    function getAssistantSourcePath(): string {
        if (!isAssistantPage) {
            return `${page.url.pathname}${page.url.search}`;
        }

        return normalizeAssistantHref(page.url.searchParams.get('context')) ?? normalizeAssistantHref(page.url.searchParams.get('from')) ?? assistantPageHref;
    }

    function getAssistantReturnHref(): string {
        return normalizeAssistantHref(page.url.searchParams.get('from')) ?? resolve('/(app)/stack');
    }

    function normalizeAssistantHref(value: null | string): string | undefined {
        if (!value?.startsWith('/')) {
            return undefined;
        }

        const url = new URL(value, page.url.origin);
        if (url.origin !== page.url.origin || url.pathname === assistantPageHref || !url.pathname.startsWith('/next/')) {
            return undefined;
        }

        return `${url.pathname}${url.search}${url.hash}`;
    }

    function getAssistantPath(context: AssistantResourceContext | undefined, fallback: string): string {
        if (context?.eventId) {
            return context.stackId
                ? `/next/stack/${encodeURIComponent(context.stackId)}/event/${encodeURIComponent(context.eventId)}`
                : `/next/event/${encodeURIComponent(context.eventId)}`;
        }

        return context?.stackId ? `/next/stack/${encodeURIComponent(context.stackId)}` : fallback;
    }

    async function openOrganizationSwitcher(): Promise<void> {
        isCommandOpen = false;
        isImpersonateOrganizationOpen = false;
        isKeyboardShortcutsOpen = false;
        isUserMenuOpen = false;
        if (singleOrganization?.id) {
            await goto(
                resolve('/(app)/organization/[organizationId]/manage', {
                    organizationId: singleOrganization.id
                })
            );
            return;
        }

        await tick();
        isOrganizationSwitcherOpen = true;
    }

    async function openUserMenu(): Promise<void> {
        isCommandOpen = false;
        isImpersonateOrganizationOpen = false;
        isKeyboardShortcutsOpen = false;
        isOrganizationSwitcherOpen = false;
        await tick();
        isUserMenuOpen = true;
    }

    async function openKeyboardShortcuts(): Promise<void> {
        isCommandOpen = false;
        isImpersonateOrganizationOpen = false;
        isOrganizationSwitcherOpen = false;
        isUserMenuOpen = false;
        await tick();
        isKeyboardShortcutsOpen = true;
    }

    async function openImpersonateOrganization(): Promise<void> {
        isCommandOpen = false;
        isKeyboardShortcutsOpen = false;
        isOrganizationSwitcherOpen = false;
        isUserMenuOpen = false;
        await tick();
        isImpersonateOrganizationOpen = true;
    }

    async function stopImpersonating(): Promise<void> {
        isCommandOpen = false;
        await goto(resolve('/(app)/stack'));
        organization.current = organizations[0]?.id;
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
    const assistantAccessQuery = getAssistantAccessQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    let assistantAccess = $derived(assistantAccessQuery.data);
    let assistantAccessState = $derived(
        resolveAssistantAccessState(
            organization.current,
            assistantAccess,
            assistantAccessQuery.isPending,
            assistantAccessQuery.isError,
            assistantAccessQuery.isFetching
        )
    );
    let isAssistantEnabled = $derived(assistantAccessState !== 'disabled');

    setAssistantControls({
        ask: (prompt) => void askAssistant(prompt),
        enabled: () => isAssistantEnabled
    });

    let organizationUsageMonth = getUtcMonthKey();
    useInterval(() => ORGANIZATION_USAGE_ROLLOVER_CHECK_INTERVAL_MS, {
        callback: () => {
            const currentMonth = getUtcMonthKey();
            if (currentMonth === organizationUsageMonth) {
                return;
            }

            organizationUsageMonth = currentMonth;
            void invalidateOrganizationUsageQueries(queryClient, organization.current);
        },
        immediate: false
    });

    async function onMessage(
        message: MessageEvent,
        organizationEventRefresher: OrganizationEventNotificationRefresher,
        projectStackRefresher: ProjectStackNotificationRefresher
    ) {
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

        if (data.type === 'PlanChanged') {
            await invalidateAssistantAccessQueries(queryClient);
        }

        if (isPlanOverageType(data)) {
            await invalidatePlanOverageQueries(queryClient, data.message);
        } else if (isEntityChangedType(data)) {
            switch (data.type) {
                case 'OrganizationChanged':
                    await invalidateOrganizationQueries(queryClient, data.message);
                    break;
                case 'PersistentEventChanged':
                    organizationEventRefresher.schedule(data.message.organization_id, data.message.change_type !== ChangeType.Removed);
                    await invalidatePersistentEventQueries(queryClient, data.message);
                    break;
                case 'ProjectChanged':
                    await invalidateProjectQueries(queryClient, data.message);
                    break;
                case 'SavedViewChanged':
                    await invalidateSavedViewQueries(queryClient, data.message);
                    break;
                case 'StackChanged':
                    organizationEventRefresher.schedule(data.message.organization_id, data.message.change_type !== ChangeType.Removed);
                    projectStackRefresher.schedule(data.message.project_id, data.message.change_type !== ChangeType.Removed);
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
                    await queryClient.invalidateQueries({
                        queryKey: [data.message.type]
                    });
                    break;
            }
        }

        // When a user is added or removed from an organization, invalidate org/project caches
        // so the UI reflects the membership change without a manual reload.
        if (data.type === 'UserMembershipChanged') {
            const membershipMessage = data.message as UserMembershipChanged;
            if (membershipMessage.organization_id) {
                const organizationChangedMessage: EntityChanged = {
                    change_type: membershipMessage.change_type,
                    data: {},
                    id: membershipMessage.organization_id,
                    organization_id: membershipMessage.organization_id,
                    type: 'Organization'
                };
                const projectChangedMessage: EntityChanged = {
                    change_type: membershipMessage.change_type,
                    data: {},
                    organization_id: membershipMessage.organization_id,
                    type: 'Project'
                };

                await Promise.all([
                    invalidateOrganizationQueries(queryClient, organizationChangedMessage),
                    invalidateProjectQueries(queryClient, projectChangedMessage)
                ]);
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
                isImpersonateOrganizationOpen ||
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
                void goto(resolve('/(app)/event'));
                return;
            }

            if (isKeyboardShortcut(e, appKeyboardShortcuts.stacks)) {
                e.preventDefault();
                void goto(resolve('/(app)/stack'));
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

        document.addEventListener('keydown', handleKeydown, {
            capture: true
        });

        const sse = new SseClient();
        const organizationEventRefresher = createOrganizationEventNotificationRefresher(queryClient);
        const projectStackRefresher = createProjectStackNotificationRefresher(queryClient);
        sse.onMessage = (message) => void onMessage(message, organizationEventRefresher, projectStackRefresher);
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
            document.removeEventListener('keydown', handleKeydown, {
                capture: true
            });
            organizationEventRefresher.cancel();
            projectStackRefresher.cancel();
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
        if (!isGlobalAdmin || !userOrganizationIds || userOrganizationIds.length === 0 || !organization.current) {
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

    function shouldRedirectToSetup(): boolean {
        const addOrganizationPath = resolve('/(app)/organization/add');
        return page.url.pathname !== addOrganizationPath && !page.url.pathname.startsWith(resolve('/(app)/system'));
    }

    // Keep selected organization synchronized with current memberships.
    $effect(() => {
        void page.url.pathname;

        if (!organizationsQuery.isSuccess) {
            return;
        }

        const hasOrganizations = organizations.length > 0;
        if (!hasOrganizations) {
            organization.current = undefined;

            if (shouldRedirectToSetup()) {
                goto(resolve('/(app)/organization/add'));
            }

            return;
        }

        const hasSelectedOrganization = !!organization.current && organizations.some((organizationItem) => organizationItem.id === organization.current);
        const hasInvalidImpersonatedOrganization = !!impersonatingOrganizationId && impersonatedOrganizationQuery.isError;
        if ((!hasSelectedOrganization && !impersonatingOrganizationId) || hasInvalidImpersonatedOrganization) {
            organization.current = organizations[0]!.id;
        }
    });

    const isImpersonating = $derived(!!impersonatedOrganization);
    const singleOrganization = $derived(!isGlobalAdmin && !isImpersonating && organizations.length === 1 ? organizations[0] : undefined);

    const savedViewsQuery = getSavedViewsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const viewToHref: Record<string, string> = {
        events: resolve('/(app)/event'),
        stacks: resolve('/(app)/stack'),
        stream: resolve('/(app)/stream')
    };

    function buildSavedViewHref(savedView: SavedView): string {
        return savedViewHref(savedView);
    }

    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = {
            authenticated: isAuthenticated,
            impersonating: isImpersonating,
            user: meQuery.data
        };
        const allRoutes = routes().filter((route) => (route.show ? route.show(context) : true));
        const organizationSettingsHref = singleOrganization?.id
            ? resolve('/(app)/organization/[organizationId]/manage', {
                  organizationId: singleOrganization.id
              })
            : undefined;

        const savedViews = (savedViewsQuery.data ?? []).filter((savedView) => !isSavedViewDeleted(savedView));

        return allRoutes.map((route) => {
            if (organizationSettingsHref && route.group === 'Settings' && route.title === 'Organizations') {
                route = {
                    ...route,
                    href: organizationSettingsHref,
                    title: 'Organization'
                };
            }

            if (savedViews.length === 0) {
                return route;
            }

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

    $effect(() => {
        if (assistantAccessQuery.isSuccess && !isAssistantEnabled) {
            isAssistantOpen = false;
        }
    });

    $effect(() => {
        if (isAssistantPage) {
            void loadAssistantPanel();
        }
    });
</script>

{#snippet setupShell()}
    <div class="flex h-screen w-full items-center justify-center px-4">
        <main class="w-full">
            <div
                in:fade={{
                    delay: 150,
                    duration: 150
                }}
                out:fade={{
                    duration: 150
                }}
            >
                {@render children()}
            </div>
        </main>
    </div>
{/snippet}

{#snippet appShell(openChat: () => void)}
    <Navbar
        assistantEnabled={isAssistantEnabled}
        isAssistantOpen={isAssistantOpen || isAssistantPage}
        openCommand={openCommandPalette}
        toggleAssistant={() => void toggleAssistantPanel()}
    />
    <Sidebar routes={filteredRoutes}>
        {#snippet header()}
            <SidebarOrganizationSwitcher
                bind:impersonateDialogOpen={isImpersonateOrganizationOpen}
                isLoading={organizationsQuery.isLoading}
                {organizations}
                {impersonatedOrganization}
                bind:open={isOrganizationSwitcherOpen}
                bind:currentOrganizationId={organization.current}
                {isGlobalAdmin}
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
        <div
            class={[
                'text-secondary-foreground flex min-h-0 min-w-0 flex-1 flex-col',
                isAssistantPage ? 'overflow-hidden' : 'scrollbar-gutter-stable overflow-x-hidden overflow-y-auto'
            ]}
        >
            <main class={isAssistantPage ? 'flex min-h-0 flex-1 flex-col' : 'flex-1 px-4 pt-4'}>
                <NavigationCommand
                    askExie={askAssistant}
                    bind:open={isCommandOpen}
                    {isChatEnabled}
                    isExieEnabled={isAssistantEnabled}
                    {isGlobalAdmin}
                    {isImpersonating}
                    {openChat}
                    openExie={openAssistantPanel}
                    {openImpersonateOrganization}
                    {openKeyboardShortcuts}
                    {openOrganizationSwitcher}
                    {openUserMenu}
                    {organizations}
                    resetKey={commandResetKey}
                    routes={filteredRoutes}
                    {stopImpersonating}
                />
                <KeyboardShortcutsDialog bind:open={isKeyboardShortcutsOpen} />

                <Notifications />

                {#if AssistantPanel && (isAssistantEnabled || isAssistantPage)}
                    <AssistantPanel
                        accessMessage={assistantAccess?.message}
                        accessState={assistantAccessState}
                        collapseHref={isAssistantPage ? assistantReturnHref : undefined}
                        expandHref={!isAssistantPage ? assistantExpandHref : undefined}
                        bind:open={isAssistantOpen}
                        minimumPlanId={assistantAccess?.minimum_plan_id}
                        mode={isAssistantPage ? 'page' : 'sheet'}
                        onAccessChanged={() => invalidateAssistantAccessQueries(queryClient)}
                        onCollapse={() => (isAssistantOpen = true)}
                        onRetryAccess={async () => {
                            await assistantAccessQuery.refetch();
                        }}
                        organizationId={organization.current}
                        path={assistantPath}
                        promptRequest={assistantPromptRequest}
                        projectId={assistantProjectId}
                    />
                {/if}

                {#if !isAssistantPage}
                    {#if showOrganizationNotifications.current}
                        <OrganizationNotifications {isChatEnabled} {openChat} {requiresPremium} premiumFeatureName={premiumPage.current} class="mb-4" />
                    {/if}

                    <div
                        in:fade={{
                            delay: 150,
                            duration: 150
                        }}
                        out:fade={{
                            duration: isAssistantPage ? 0 : 150
                        }}
                    >
                        {@render children()}
                    </div>
                {/if}
            </main>

            {#if !isAssistantPage}
                <Footer></Footer>
            {/if}
        </div>
    </div>
{/snippet}

{#if isAuthenticated}
    <IntercomShell
        appId={intercomAppId || undefined}
        bootOptions={intercomBootOptions}
        onUnreadCountChange={onIntercomUnreadCountChange}
        routeKey={getIntercomRouteKey(page.route.id, page.url.pathname)}
    >
        {#snippet children(openChat)}
            {#if isSetupPage}
                {@render setupShell()}
            {:else}
                {@render appShell(openChat)}
            {/if}
        {/snippet}
    </IntercomShell>

    <ChangePlanDialogHost />
    <UpgradeRequiredDialog />
{/if}

<Telemetry userId={isAuthenticated ? meQuery.data?.email_address : undefined} userName={isAuthenticated ? meQuery.data?.full_name : undefined} />
