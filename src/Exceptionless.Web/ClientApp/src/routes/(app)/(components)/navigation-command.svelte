<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { ViewProject } from '$features/projects/models';
    import type { FetchClientResponse } from '@foundatiofx/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import * as Command from '$comp/ui/command';
    import { logout } from '$features/auth/api.svelte';
    import { accessToken } from '$features/auth/index.svelte';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { buildEventDetailsHref, type EventSummaryModel, type StackSummaryModel, type SummaryTemplateKeys } from '$features/events/components/summary/index';
    import { addOrganizationUser } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { resetData } from '$features/projects/api.svelte';
    import ResetProjectDataDialog from '$features/projects/components/dialogs/reset-project-data-dialog.svelte';
    import ProjectCommandActions, { type ProjectActionId } from '$features/projects/components/project-command-actions.svelte';
    import { appKeyboardShortcuts, formatKeyboardShortcut, type ShortcutKey } from '$features/shared/keyboard-shortcuts';
    import InviteUserDialog from '$features/users/components/invite-user-dialog.svelte';
    import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
    import { ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
    import Activity from '@lucide/svelte/icons/activity';
    import Bot from '@lucide/svelte/icons/bot';
    import Building2 from '@lucide/svelte/icons/building-2';
    import CircleHelp from '@lucide/svelte/icons/circle-help';
    import CircleUserRound from '@lucide/svelte/icons/circle-user-round';
    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';
    import Keyboard from '@lucide/svelte/icons/keyboard';
    import Stacks from '@lucide/svelte/icons/layers';
    import LogOut from '@lucide/svelte/icons/log-out';
    import Plus from '@lucide/svelte/icons/plus';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';
    import Search from '@lucide/svelte/icons/search';
    import SunMoon from '@lucide/svelte/icons/sun-moon';
    import UserPlus from '@lucide/svelte/icons/user-plus';
    import Users from '@lucide/svelte/icons/users';
    import { createQuery, useQueryClient } from '@tanstack/svelte-query';
    import { toggleMode } from 'mode-watcher';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { NavigationItem } from '../../routes.svelte';

    type CommandNavigationItem = {
        group: string;
        href: string;
        icon: NavigationItem['icon'];
        keywords?: string[];
        openInNewTab?: boolean;
        parentTitle?: string;
        shortcut?: readonly ShortcutKey[];
        title: string;
        value: string;
    };

    type Props = {
        askExie: (prompt: string) => Promise<void> | void;
        isChatEnabled: boolean;
        isExieEnabled: boolean;
        isGlobalAdmin: boolean;
        isImpersonating: boolean;
        open: boolean;
        openChat: () => void;
        openExie: () => Promise<void> | void;
        openImpersonateOrganization: () => Promise<void> | void;
        openKeyboardShortcuts: () => Promise<void> | void;
        openOrganizationSwitcher: () => Promise<void> | void;
        openUserMenu: () => Promise<void> | void;
        organizations: ViewOrganization[];
        resetKey: number;
        routes: NavigationItem[];
        stopImpersonating: () => Promise<void> | void;
    };

    type CommandSearchResult = EventSummaryModel<SummaryTemplateKeys> | StackSummaryModel<SummaryTemplateKeys>;

    const EXIE_ERROR_TRENDS_PROMPT =
        'Analyze error trends in the current context over the last 7 days. Highlight spikes, regressions, and the issues that deserve attention first.';
    const EXIE_TRIAGE_PROMPT =
        'Triage the most important recent errors in the current context. Summarize their impact, likely causes, and the next investigation steps.';
    const COMMAND_SEARCH_RESULT_LIMIT = 3;
    const COMMAND_SEARCH_REQUEST_LIMIT = COMMAND_SEARCH_RESULT_LIMIT + 1;
    const COMMAND_SEARCH_MIN_LENGTH = 2;
    const COMMAND_SEARCH_TIME_RANGE = '[now-7d TO now]';

    let {
        askExie,
        isChatEnabled,
        isExieEnabled,
        isGlobalAdmin,
        isImpersonating,
        open = $bindable(),
        openChat,
        openExie,
        openImpersonateOrganization,
        openKeyboardShortcuts,
        openOrganizationSwitcher,
        openUserMenu,
        organizations,
        resetKey,
        routes,
        stopImpersonating
    }: Props = $props();
    let searchText = $state('');
    let debouncedSearchText = $state('');
    let selectedProjectActionId = $state<ProjectActionId>();
    let selectingProject = $state(false);

    const client = useFetchClient();
    const queryClient = useQueryClient();
    const hasSearchText = $derived(debouncedSearchText.length >= COMMAND_SEARCH_MIN_LENGTH);
    const switchableOrganizations = $derived(organizations.filter((organizationItem) => organizationItem.id !== organization.current));

    $effect(() => {
        const trimmedSearchText = searchText.trim();
        if (trimmedSearchText.length < COMMAND_SEARCH_MIN_LENGTH) {
            debouncedSearchText = '';
            return;
        }

        const timeout = window.setTimeout(() => {
            debouncedSearchText = trimmedSearchText;
        }, 150);

        return () => {
            window.clearTimeout(timeout);
        };
    });

    const eventSearchQuery = createQuery<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>, ProblemDetails>(() => ({
        enabled: () => open && !selectingProject && !!accessToken.current && !!organization.current && hasSearchText,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            return client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    filter: debouncedSearchText,
                    limit: COMMAND_SEARCH_REQUEST_LIMIT,
                    mode: 'summary',
                    time: COMMAND_SEARCH_TIME_RANGE
                },
                signal
            });
        },
        queryKey: ['navigation-command', 'events', organization.current, debouncedSearchText]
    }));

    const stackSearchQuery = createQuery<FetchClientResponse<StackSummaryModel<SummaryTemplateKeys>[]>, ProblemDetails>(() => ({
        enabled: () => open && !selectingProject && !!accessToken.current && !!organization.current && hasSearchText,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            return client.getJSON<StackSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    filter: debouncedSearchText,
                    limit: COMMAND_SEARCH_REQUEST_LIMIT,
                    mode: 'stack_frequent',
                    time: COMMAND_SEARCH_TIME_RANGE
                },
                signal
            });
        },
        queryKey: ['navigation-command', 'stacks', organization.current, debouncedSearchText]
    }));

    const eventMatches = $derived((eventSearchQuery.data?.data ?? []).slice(0, COMMAND_SEARCH_RESULT_LIMIT));
    const stackMatches = $derived((stackSearchQuery.data?.data ?? []).slice(0, COMMAND_SEARCH_RESULT_LIMIT));
    const hasMoreEventMatches = $derived((eventSearchQuery.data?.data?.length ?? 0) > COMMAND_SEARCH_RESULT_LIMIT);
    const hasMoreStackMatches = $derived((stackSearchQuery.data?.data?.length ?? 0) > COMMAND_SEARCH_RESULT_LIMIT);
    const isRemoteSearchPending = $derived(eventSearchQuery.isPending || stackSearchQuery.isPending);
    const showEventSearchResults = $derived(eventMatches.length > 0 || hasMoreEventMatches);
    const showStackSearchResults = $derived(stackMatches.length > 0 || hasMoreStackMatches);
    const showRemoteSearchResults = $derived(showEventSearchResults || showStackSearchResults);

    $effect(() => {
        if (resetKey >= 0) {
            searchText = '';
            debouncedSearchText = '';
            selectedProjectActionId = undefined;
            selectingProject = false;
        }
    });

    function getCommandGroup(route: NavigationItem): string {
        return route.group === 'Dashboards' ? route.title : route.group;
    }

    function getCommandTitle(route: NavigationItem): string {
        return route.group === 'Dashboards' ? `All ${route.title}` : route.title;
    }

    function getCommandValue(...parts: Array<string | undefined>): string {
        return parts.filter(Boolean).join(' ');
    }

    function filterCommandItem(value: string, search: string, keywords?: string[]): number {
        const normalizedSearch = search.trim().toLocaleLowerCase();
        if (!normalizedSearch) {
            return 1;
        }

        const searchableText = [value, ...(keywords ?? [])].join(' ').toLocaleLowerCase();
        return searchableText.includes(normalizedSearch) ? 1 : 0;
    }

    function buildSearchHref(path: string, searchText: string): string {
        const params = new URLSearchParams({
            filter: searchText,
            limit: '20',
            time: ''
        });

        return `${path}?${params.toString()}`;
    }

    function getResultTitle(result: CommandSearchResult): string {
        if ('title' in result && result.title) {
            return result.title;
        }

        const data = result.data as Record<string, unknown>;
        const values = [data.Type, data.Method, data.Source, data.Name, data.Message, data.Path].filter(
            (value): value is string => typeof value === 'string' && value.length > 0
        );

        return values.join(' ') || result.id;
    }

    function getResultDescription(result: CommandSearchResult): string | undefined {
        const data = result.data as Record<string, unknown>;
        const values = [data.Identity, data.Source, data.Path].filter((value): value is string => typeof value === 'string' && value.length > 0);

        return values.join(' · ') || undefined;
    }

    function getResultValue(group: 'Event' | 'Stack', result: CommandSearchResult): string {
        return getCommandValue(group, debouncedSearchText, getResultTitle(result), getResultDescription(result), result.id);
    }

    function getEventHref(result: CommandSearchResult): string {
        return buildEventDetailsHref(result.id);
    }

    function getStackHref(result: CommandSearchResult): string {
        return resolve('/(app)/stack/[stackId=objectid]', {
            stackId: result.id
        });
    }

    const eventSearchHref = $derived(buildSearchHref(resolve('/(app)/event'), debouncedSearchText));
    const stackSearchHref = $derived(buildSearchHref(resolve('/(app)/stack'), debouncedSearchText));

    const commandRoutes = $derived(
        routes.flatMap((route) => {
            const group = getCommandGroup(route);
            const title = getCommandTitle(route);
            const items: CommandNavigationItem[] = [
                {
                    group,
                    href: route.href,
                    icon: route.icon,
                    keywords: route.keywords,
                    openInNewTab: route.openInNewTab,
                    shortcut: route.shortcut,
                    title,
                    value: getCommandValue(group, route.title, title)
                }
            ];

            if (route.children?.length) {
                items.push(
                    ...route.children.map((child) => ({
                        group,
                        href: child.href,
                        icon: route.icon,
                        parentTitle: route.group === 'Dashboards' ? undefined : route.title,
                        title: child.title,
                        value: getCommandValue(group, route.title, child.title)
                    }))
                );
            }

            return items;
        })
    );

    const groupedRoutes = $derived(
        Object.entries(Object.groupBy(commandRoutes, (item: CommandNavigationItem) => item.group)).reduce(
            (acc, [key, value]) => {
                if (value) {
                    acc[key] = value;
                }

                return acc;
            },
            {} as Record<string, CommandNavigationItem[]>
        )
    );

    function closeCommandWindow() {
        open = false;
    }

    function resetCommandSearch(): void {
        searchText = '';
        debouncedSearchText = '';
        commandValue = '';
    }

    let resetProjectTarget = $state<ViewProject>();
    let showResetProjectDataDialog = $state(false);
    const resetProjectDataMutation = resetData({
        route: {
            get id() {
                return resetProjectTarget?.id ?? '';
            }
        }
    });

    function openResetProjectDataDialog(project: ViewProject): void {
        resetProjectTarget = project;
        showResetProjectDataDialog = true;
    }

    async function resetProjectData(): Promise<void> {
        try {
            await resetProjectDataMutation.mutateAsync();
            toast.success(`Successfully queued "${resetProjectTarget?.name}" for data reset.`);
        } catch (error) {
            toast.error(`Failed to reset data for "${resetProjectTarget?.name}". Please try again.`);
            throw error;
        }
    }

    async function switchOrganization(): Promise<void> {
        closeCommandWindow();
        await openOrganizationSwitcher();
    }

    async function openExieAssistant(): Promise<void> {
        closeCommandWindow();
        await openExie();
    }

    async function askExieAssistant(prompt: string): Promise<void> {
        closeCommandWindow();
        await askExie(prompt);
    }

    let showInviteUserDialog = $state(false);
    const addOrganizationUserMutation = addOrganizationUser({
        route: {
            get organizationId() {
                return organization.current ?? '';
            }
        }
    });

    async function openInviteUserDialog(): Promise<void> {
        closeCommandWindow();
        await tick();
        showInviteUserDialog = true;
    }

    async function inviteUser(email: string): Promise<void> {
        try {
            await addOrganizationUserMutation.mutateAsync(email);
            toast.success('User invited successfully');
        } catch (error: unknown) {
            if (showBillingDialogOnUpgradeProblem(error, organization.current, () => inviteUser(email))) {
                return;
            }

            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toast.error(`An error occurred while trying to invite the user: ${message}`);
            throw error;
        }
    }

    async function openCurrentUserMenu(): Promise<void> {
        closeCommandWindow();
        await openUserMenu();
    }

    async function openKeyboardShortcutsDialog(): Promise<void> {
        closeCommandWindow();
        await openKeyboardShortcuts();
    }

    async function switchToOrganization(organizationItem: ViewOrganization): Promise<void> {
        closeCommandWindow();
        organization.current = organizationItem.id;
        await goto(resolve('/'));
    }

    async function openImpersonateOrganizationDialog(): Promise<void> {
        closeCommandWindow();
        await openImpersonateOrganization();
    }

    async function stopImpersonatingOrganization(): Promise<void> {
        closeCommandWindow();
        await stopImpersonating();
    }

    function openSupportChat(): void {
        closeCommandWindow();
        openChat();
    }

    function toggleTheme(): void {
        closeCommandWindow();
        toggleMode();
    }

    let isRefreshing = $state(false);
    async function refreshCurrentView(): Promise<void> {
        closeCommandWindow();
        isRefreshing = true;
        document.dispatchEvent(
            new CustomEvent('refresh', {
                bubbles: true,
                detail: 'Command Palette'
            })
        );

        try {
            await queryClient.refetchQueries({
                type: 'active'
            });
            toast.success('Refreshed the current view.');
        } finally {
            isRefreshing = false;
        }
    }

    async function logOutCurrentUser(): Promise<void> {
        closeCommandWindow();
        await logout(queryClient, client);
        await goto(resolve('/(auth)/login'));
    }

    const PAGE_JUMP_SIZE = 7;
    let commandValue = $state('');

    function handleKeydown(event: KeyboardEvent): void {
        if (event.key !== 'PageDown' && event.key !== 'PageUp') {
            return;
        }

        event.preventDefault();

        const root = event.currentTarget;
        if (!(root instanceof HTMLElement)) {
            return;
        }

        const items = Array.from(root.querySelectorAll<HTMLElement>('[data-command-item]:not([aria-disabled="true"])'));
        const visibleItems = items.filter((item) => {
            const group = item.closest('[data-command-group]');
            return !group?.hasAttribute('hidden');
        });

        if (visibleItems.length === 0) {
            return;
        }

        const currentIndex = visibleItems.findIndex((item) => item.hasAttribute('data-selected'));
        let targetIndex: number;

        if (event.key === 'PageDown') {
            targetIndex = currentIndex === -1 ? PAGE_JUMP_SIZE - 1 : Math.min(currentIndex + PAGE_JUMP_SIZE, visibleItems.length - 1);
        } else {
            targetIndex = currentIndex === -1 ? 0 : Math.max(currentIndex - PAGE_JUMP_SIZE, 0);
        }

        const targetValue = visibleItems[targetIndex]?.getAttribute('data-value');
        if (targetValue) {
            commandValue = targetValue;
            visibleItems[targetIndex]?.scrollIntoView({
                block: 'nearest'
            });
        }
    }
</script>

{#key resetKey}
    <Command.Dialog bind:open bind:value={commandValue} filter={filterCommandItem} onkeydown={handleKeydown}>
        <Command.Input bind:value={searchText} placeholder={selectingProject ? 'Select a project...' : 'Search or jump to...'} />
        <Command.List>
            <Command.Empty>{hasSearchText && isRemoteSearchPending ? 'Searching...' : 'No results found.'}</Command.Empty>
            {#if !selectingProject && hasSearchText && showRemoteSearchResults}
                {#key debouncedSearchText}
                    {#if showEventSearchResults}
                        <Command.Group heading="Events" value="Search Events">
                            {#each eventMatches as event (event.id)}
                                <Command.LinkItem href={getEventHref(event)} onclick={closeCommandWindow} value={getResultValue('Event', event)}>
                                    <Activity />
                                    <div class="flex min-w-0 flex-col">
                                        <span class="truncate">{getResultTitle(event)}</span>
                                        {#if getResultDescription(event)}
                                            <span class="text-muted-foreground truncate text-xs">{getResultDescription(event)}</span>
                                        {/if}
                                    </div>
                                </Command.LinkItem>
                            {/each}
                            {#if hasMoreEventMatches}
                                <Command.LinkItem href={eventSearchHref} onclick={closeCommandWindow} value={`View all events ${debouncedSearchText}`}>
                                    <Search />
                                    <span>View all matching events</span>
                                </Command.LinkItem>
                            {/if}
                        </Command.Group>
                    {/if}
                    {#if showEventSearchResults && showStackSearchResults}
                        <Command.Separator />
                    {/if}
                    {#if showStackSearchResults}
                        <Command.Group heading="Stacks" value="Search Stacks">
                            {#each stackMatches as stack (stack.id)}
                                <Command.LinkItem href={getStackHref(stack)} onclick={closeCommandWindow} value={getResultValue('Stack', stack)}>
                                    <Stacks />
                                    <div class="flex min-w-0 flex-col">
                                        <span class="truncate">{getResultTitle(stack)}</span>
                                        {#if getResultDescription(stack)}
                                            <span class="text-muted-foreground truncate text-xs">{getResultDescription(stack)}</span>
                                        {/if}
                                    </div>
                                </Command.LinkItem>
                            {/each}
                            {#if hasMoreStackMatches}
                                <Command.LinkItem href={stackSearchHref} onclick={closeCommandWindow} value={`View all stacks ${debouncedSearchText}`}>
                                    <Search />
                                    <span>View all matching stacks</span>
                                </Command.LinkItem>
                            {/if}
                        </Command.Group>
                    {/if}
                {/key}
                <Command.Separator />
            {/if}
            <ProjectCommandActions
                {open}
                bind:selectingProject
                onReset={openResetProjectDataDialog}
                onSearchReset={resetCommandSearch}
                onSelect={closeCommandWindow}
                resetPending={resetProjectDataMutation.isPending}
                bind:selectedActionId={selectedProjectActionId}
            />
            {#if !selectingProject}
                {#if isExieEnabled}
                    <Command.Group heading="Exie" value="Exie Assistant">
                        <Command.Item value="Ask Exie open assistant AI chat" onSelect={() => void openExieAssistant()}>
                            <Bot />
                            <span>Ask Exie</span>
                        </Command.Item>
                        <Command.Item value="Exie Triage Recent Errors investigate issues stacks" onSelect={() => void askExieAssistant(EXIE_TRIAGE_PROMPT)}>
                            <Activity />
                            <span>Triage Recent Errors</span>
                        </Command.Item>
                        <Command.Item
                            value="Exie Analyze Error Trends seven days spikes regressions"
                            onSelect={() => void askExieAssistant(EXIE_ERROR_TRENDS_PROMPT)}
                        >
                            <Stacks />
                            <span>Analyze Error Trends</span>
                        </Command.Item>
                    </Command.Group>
                    <Command.Separator />
                {/if}
                {#each Object.entries(groupedRoutes) as [group, items], index (group)}
                    <Command.Group heading={group}>
                        {#each items as route (route.href)}
                            <Command.LinkItem
                                href={route.href}
                                keywords={route.keywords}
                                onclick={closeCommandWindow}
                                rel={route.openInNewTab ? 'noreferrer' : undefined}
                                target={route.openInNewTab ? '_blank' : undefined}
                                value={route.value}
                            >
                                {#if route.icon}
                                    {@const Icon = route.icon}
                                    <Icon />
                                {/if}
                                <div class="flex min-w-0 flex-col">
                                    <span class="truncate">{route.title}</span>
                                    {#if route.parentTitle}
                                        <span class="text-muted-foreground text-xs">{route.parentTitle}</span>
                                    {/if}
                                </div>
                                {#if route.shortcut}
                                    <Command.Shortcut>{formatKeyboardShortcut(route.shortcut)}</Command.Shortcut>
                                {/if}
                            </Command.LinkItem>
                        {/each}
                    </Command.Group>
                    {#if group === 'Sessions'}
                        <Command.Separator />
                        <Command.Group heading="Organizations">
                            {#each switchableOrganizations as organizationItem (organizationItem.id)}
                                <Command.Item
                                    value={`Switch to Organization ${organizationItem.name}`}
                                    onSelect={() => void switchToOrganization(organizationItem)}
                                >
                                    <Building2 />
                                    <span>Switch to {organizationItem.name}</span>
                                </Command.Item>
                            {/each}
                            <Command.Item value="Switch Organization organizations org" onSelect={() => void switchOrganization()}>
                                <Building2 />
                                <span>Switch Organization</span>
                                <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.switchOrganization.keys)}</Command.Shortcut>
                            </Command.Item>
                            {#if organization.current}
                                <Command.LinkItem
                                    href={resolve('/(app)/organization/[organizationId]/users', {
                                        organizationId: organization.current
                                    })}
                                    onclick={closeCommandWindow}
                                    value="View Organization Users manage members team user list"
                                >
                                    <Users />
                                    <span>View Organization Users</span>
                                </Command.LinkItem>
                                <Command.Item value="Invite User add member organization team" onSelect={() => void openInviteUserDialog()}>
                                    <UserPlus />
                                    <span>Invite User</span>
                                </Command.Item>
                            {/if}
                            <Command.LinkItem
                                href={resolve('/(app)/organization/add')}
                                onclick={closeCommandWindow}
                                value="Add Organization create new organization"
                            >
                                <Plus />
                                <span>Add Organization</span>
                            </Command.LinkItem>
                            {#if isGlobalAdmin}
                                {#if isImpersonating}
                                    <Command.Item value="Stop Impersonating Organization admin" onSelect={() => void stopImpersonatingOrganization()}>
                                        <EyeOff />
                                        <span>Stop Impersonating</span>
                                    </Command.Item>
                                {:else}
                                    <Command.Item value="Impersonate Organization admin" onSelect={() => void openImpersonateOrganizationDialog()}>
                                        <Eye />
                                        <span>Impersonate Organization</span>
                                    </Command.Item>
                                {/if}
                            {/if}
                        </Command.Group>
                        <Command.Separator />
                        <Command.Group heading="Actions">
                            {#if isChatEnabled}
                                <Command.Item value="Chat with Support help intercom" onSelect={openSupportChat}>
                                    <CircleHelp />
                                    <span>Chat with Support</span>
                                </Command.Item>
                            {/if}
                            <Command.Item value="Toggle Theme dark light mode appearance" onSelect={toggleTheme}>
                                <SunMoon />
                                <span>Toggle Theme</span>
                            </Command.Item>
                            <Command.Item disabled={isRefreshing} value="Refresh Current View reload data" onSelect={() => void refreshCurrentView()}>
                                <RefreshCw class={isRefreshing ? 'animate-spin' : undefined} />
                                <span>Refresh Current View</span>
                            </Command.Item>
                            <Command.Item value="Open User Menu account profile current user" onSelect={() => void openCurrentUserMenu()}>
                                <CircleUserRound />
                                <span>Open User Menu</span>
                                <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.userMenu.keys)}</Command.Shortcut>
                            </Command.Item>
                            <Command.Item value="Keyboard Shortcuts help shortcuts" onSelect={() => void openKeyboardShortcutsDialog()}>
                                <Keyboard />
                                <span>Keyboard Shortcuts</span>
                                <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.keyboardShortcuts.keys)}</Command.Shortcut>
                            </Command.Item>
                            <Command.Item value="Log Out sign out logout" onSelect={() => void logOutCurrentUser()}>
                                <LogOut />
                                <span>Log Out</span>
                            </Command.Item>
                        </Command.Group>
                    {/if}
                    {#if index !== Object.keys(groupedRoutes).length - 1}
                        <Command.Separator />
                    {/if}
                {/each}
            {/if}
        </Command.List>
    </Command.Dialog>
{/key}

{#if resetProjectTarget}
    <ResetProjectDataDialog bind:open={showResetProjectDataDialog} name={resetProjectTarget.name} reset={resetProjectData} />
{/if}

<InviteUserDialog bind:open={showInviteUserDialog} {inviteUser} />
