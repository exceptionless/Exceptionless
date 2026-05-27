<script lang="ts">
    import type { EventSummaryModel, StackSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';
    import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';

    import { resolve } from '$app/paths';
    import * as Command from '$comp/ui/command';
    import { accessToken } from '$features/auth/index.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { appKeyboardShortcuts, formatKeyboardShortcut, type ShortcutKey } from '$features/shared/keyboard-shortcuts';
    import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import Activity from '@lucide/svelte/icons/activity';
    import Bug from '@lucide/svelte/icons/bug';
    import Building2 from '@lucide/svelte/icons/building-2';
    import CircleUserRound from '@lucide/svelte/icons/circle-user-round';
    import Keyboard from '@lucide/svelte/icons/keyboard';
    import Search from '@lucide/svelte/icons/search';
    import { createQuery } from '@tanstack/svelte-query';

    import type { NavigationItem } from '../../routes.svelte';

    type CommandNavigationItem = {
        group: string;
        href: string;
        icon: NavigationItem['icon'];
        openInNewTab?: boolean;
        parentTitle?: string;
        shortcut?: readonly ShortcutKey[];
        title: string;
        value: string;
    };

    type Props = {
        open: boolean;
        openKeyboardShortcuts: () => Promise<void> | void;
        openOrganizationSwitcher: () => Promise<void> | void;
        openUserMenu: () => Promise<void> | void;
        resetKey: number;
        routes: NavigationItem[];
    };

    type CommandSearchResult = EventSummaryModel<SummaryTemplateKeys> | StackSummaryModel<SummaryTemplateKeys>;

    const COMMAND_SEARCH_RESULT_LIMIT = 3;
    const COMMAND_SEARCH_REQUEST_LIMIT = COMMAND_SEARCH_RESULT_LIMIT + 1;
    const COMMAND_SEARCH_MIN_LENGTH = 2;
    const COMMAND_SEARCH_TIME_RANGE = '[now-7d TO now]';

    let { open = $bindable(), openKeyboardShortcuts, openOrganizationSwitcher, openUserMenu, resetKey, routes }: Props = $props();
    let searchText = $state('');
    let debouncedSearchText = $state('');

    const client = useFetchClient();
    const hasSearchText = $derived(debouncedSearchText.length >= COMMAND_SEARCH_MIN_LENGTH);

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
        enabled: () => open && !!accessToken.current && !!organization.current && hasSearchText,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            return client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
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

    const issueSearchQuery = createQuery<FetchClientResponse<StackSummaryModel<SummaryTemplateKeys>[]>, ProblemDetails>(() => ({
        enabled: () => open && !!accessToken.current && !!organization.current && hasSearchText,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            return client.getJSON<StackSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    filter: debouncedSearchText,
                    limit: COMMAND_SEARCH_REQUEST_LIMIT,
                    mode: 'stack_frequent',
                    time: COMMAND_SEARCH_TIME_RANGE
                },
                signal
            });
        },
        queryKey: ['navigation-command', 'issues', organization.current, debouncedSearchText]
    }));

    const eventMatches = $derived((eventSearchQuery.data?.data ?? []).slice(0, COMMAND_SEARCH_RESULT_LIMIT));
    const issueMatches = $derived((issueSearchQuery.data?.data ?? []).slice(0, COMMAND_SEARCH_RESULT_LIMIT));
    const hasMoreEventMatches = $derived((eventSearchQuery.data?.data?.length ?? 0) > COMMAND_SEARCH_RESULT_LIMIT);
    const hasMoreIssueMatches = $derived((issueSearchQuery.data?.data?.length ?? 0) > COMMAND_SEARCH_RESULT_LIMIT);
    const showEventSearchResults = $derived(eventSearchQuery.isPending || eventMatches.length > 0 || hasMoreEventMatches);
    const showIssueSearchResults = $derived(issueSearchQuery.isPending || issueMatches.length > 0 || hasMoreIssueMatches);
    const showRemoteSearchResults = $derived(showEventSearchResults || showIssueSearchResults);

    $effect(() => {
        if (resetKey >= 0) {
            searchText = '';
            debouncedSearchText = '';
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

    function getResultValue(group: 'Event' | 'Issue', result: CommandSearchResult): string {
        return getCommandValue(group, debouncedSearchText, getResultTitle(result), getResultDescription(result), result.id);
    }

    function getEventHref(result: CommandSearchResult): string {
        return resolve('/(app)/events/[eventId=objectid]', { eventId: result.id });
    }

    function getIssueHref(result: CommandSearchResult): string {
        return resolve('/(app)/issues/[stackId=objectid]', { stackId: result.id });
    }

    const eventSearchHref = $derived(buildSearchHref(resolve('/(app)/events'), debouncedSearchText));
    const issueSearchHref = $derived(buildSearchHref(resolve('/(app)/issues'), debouncedSearchText));

    const commandRoutes = $derived(
        routes.flatMap((route) => {
            const group = getCommandGroup(route);
            const title = getCommandTitle(route);
            const items: CommandNavigationItem[] = [
                {
                    group,
                    href: route.href,
                    icon: route.icon,
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

    async function switchOrganization(): Promise<void> {
        closeCommandWindow();
        await openOrganizationSwitcher();
    }

    async function openCurrentUserMenu(): Promise<void> {
        closeCommandWindow();
        await openUserMenu();
    }

    async function openKeyboardShortcutsDialog(): Promise<void> {
        closeCommandWindow();
        await openKeyboardShortcuts();
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
            visibleItems[targetIndex]?.scrollIntoView({ block: 'nearest' });
        }
    }
</script>

{#key resetKey}
    <Command.Dialog bind:open bind:value={commandValue} filter={filterCommandItem} onkeydown={handleKeydown}>
        <Command.Input bind:value={searchText} placeholder="Search or jump to..." />
        <Command.List>
            <Command.Empty>No results found.</Command.Empty>
            {#if hasSearchText && showRemoteSearchResults}
                {#key debouncedSearchText}
                    {#if showEventSearchResults}
                        <Command.Group heading="Events" value="Search Events">
                            {#if eventSearchQuery.isPending}
                                <Command.Item disabled value={`Searching events ${debouncedSearchText}`}>
                                    <Activity />
                                    <span>Searching events...</span>
                                </Command.Item>
                            {:else}
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
                            {/if}
                        </Command.Group>
                    {/if}
                    {#if showEventSearchResults && showIssueSearchResults}
                        <Command.Separator />
                    {/if}
                    {#if showIssueSearchResults}
                        <Command.Group heading="Issues" value="Search Issues">
                            {#if issueSearchQuery.isPending}
                                <Command.Item disabled value={`Searching issues ${debouncedSearchText}`}>
                                    <Bug />
                                    <span>Searching issues...</span>
                                </Command.Item>
                            {:else}
                                {#each issueMatches as issue (issue.id)}
                                    <Command.LinkItem href={getIssueHref(issue)} onclick={closeCommandWindow} value={getResultValue('Issue', issue)}>
                                        <Bug />
                                        <div class="flex min-w-0 flex-col">
                                            <span class="truncate">{getResultTitle(issue)}</span>
                                            {#if getResultDescription(issue)}
                                                <span class="text-muted-foreground truncate text-xs">{getResultDescription(issue)}</span>
                                            {/if}
                                        </div>
                                    </Command.LinkItem>
                                {/each}
                                {#if hasMoreIssueMatches}
                                    <Command.LinkItem href={issueSearchHref} onclick={closeCommandWindow} value={`View all issues ${debouncedSearchText}`}>
                                        <Search />
                                        <span>View all matching issues</span>
                                    </Command.LinkItem>
                                {/if}
                            {/if}
                        </Command.Group>
                    {/if}
                {/key}
                <Command.Separator />
            {/if}
            {#each Object.entries(groupedRoutes) as [group, items], index (group)}
                <Command.Group heading={group}>
                    {#each items as route (route.href)}
                        <Command.LinkItem
                            href={route.href}
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
                        <Command.Item value="Switch Organization organizations org" onSelect={() => void switchOrganization()}>
                            <Building2 />
                            <span>Switch Organization</span>
                            <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.switchOrganization.keys)}</Command.Shortcut>
                        </Command.Item>
                    </Command.Group>
                    <Command.Separator />
                    <Command.Group heading="User">
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
                    </Command.Group>
                {/if}
                {#if index !== Object.keys(groupedRoutes).length - 1}
                    <Command.Separator />
                {/if}
            {/each}
        </Command.List>
    </Command.Dialog>
{/key}
