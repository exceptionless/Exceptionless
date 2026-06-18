<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { Gravatar } from '$features/users/gravatar.svelte';
    import type { ViewCurrentUser } from '$features/users/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import GitHubIcon from '$comp/icons/GitHubIcon.svelte';
    import * as Avatar from '$comp/ui/avatar/index';
    import { Badge } from '$comp/ui/badge';
    import * as DropdownMenu from '$comp/ui/dropdown-menu/index';
    import * as Sidebar from '$comp/ui/sidebar/index';
    import { useSidebar } from '$comp/ui/sidebar/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import { logout } from '$features/auth/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { apiReferenceHref, documentationHref, githubRepositoryHref, supportIssuesHref } from '$features/shared/help-links';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import BadgeCheck from '@lucide/svelte/icons/badge-check';
    import Bell from '@lucide/svelte/icons/bell';
    import BookOpen from '@lucide/svelte/icons/book-open';
    import Braces from '@lucide/svelte/icons/braces';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import Help from '@lucide/svelte/icons/circle-help';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import LogOut from '@lucide/svelte/icons/log-out';
    import Plus from '@lucide/svelte/icons/plus';
    import Settings from '@lucide/svelte/icons/settings';
    import { useQueryClient } from '@tanstack/svelte-query';

    interface Props {
        gravatar: Gravatar;
        intercomUnreadCount: number;
        isChatEnabled: boolean;
        isLoading: boolean;
        open?: boolean;
        openChat: () => void;
        openKeyboardShortcuts: () => Promise<void> | void;
        organizations?: ViewOrganization[];
        user: undefined | ViewCurrentUser;
    }

    let {
        gravatar,
        intercomUnreadCount = 0,
        isChatEnabled,
        isLoading,
        open = $bindable(false),
        openChat,
        openKeyboardShortcuts,
        organizations = [],
        user
    }: Props = $props();
    const sidebar = useSidebar();
    const client = useFetchClient();
    const queryClient = useQueryClient();
    const currentOrganizationId = $derived(organizations.find((organizationItem) => organizationItem.id === organization.current)?.id);

    function getUnreadCountLabel(unreadCount: number): string {
        return unreadCount > 99 ? '99+' : unreadCount.toString();
    }

    function onMenuClick() {
        if (sidebar.isMobile) {
            sidebar.toggle();
        }
    }

    function onChatClick() {
        onMenuClick();
        openChat();
    }

    function onKeyboardShortcutsClick() {
        onMenuClick();
        void openKeyboardShortcuts();
    }

    function navigateTo(href: string): void {
        onMenuClick();
        void goto(href);
    }

    async function onLogout(): Promise<void> {
        onMenuClick();
        await logout(queryClient, client);
        await goto(resolve('/(auth)/login'));
    }

    function openExternalLink(href: string): void {
        onMenuClick();
        window.open(href, '_blank', 'noopener,noreferrer');
    }
</script>

{#if isLoading}
    <Sidebar.Menu>
        <Sidebar.MenuItem>
            <Sidebar.MenuButton size="lg" class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground">
                <Skeleton class="size-8 min-w-8 rounded-lg" />
                <div class="grid flex-1 gap-1">
                    <Skeleton class="h-4 w-full" />
                    <Skeleton class="h-3 w-full" />
                </div>
            </Sidebar.MenuButton>
        </Sidebar.MenuItem>
    </Sidebar.Menu>
{:else}
    {#if isChatEnabled}
        <Sidebar.Menu>
            <Sidebar.MenuItem>
                <Sidebar.MenuButton
                    size="lg"
                    class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
                    onclick={onChatClick}
                >
                    <Help class="size-4" aria-hidden="true" />
                    <div class="grid flex-1 gap-0.5 text-left">
                        <span class="text-sm leading-none font-medium">Chat with Support</span>
                    </div>
                    {#if intercomUnreadCount > 0}
                        <Sidebar.MenuBadge>{getUnreadCountLabel(intercomUnreadCount)}</Sidebar.MenuBadge>
                    {/if}
                </Sidebar.MenuButton>
            </Sidebar.MenuItem>
        </Sidebar.Menu>
    {/if}
    <Sidebar.Menu>
        <Sidebar.MenuItem>
            <DropdownMenu.Root bind:open>
                <DropdownMenu.Trigger>
                    {#snippet child({ props })}
                        <Sidebar.MenuButton size="lg" class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground" {...props}>
                            <Avatar.Root class="size-8 rounded-lg" title="Profile Image">
                                {#await gravatar.src}
                                    <Avatar.Fallback class="rounded-lg">{gravatar.initials}</Avatar.Fallback>
                                {:then src}
                                    {#if src}
                                        <Avatar.Image alt={user ? `${user.full_name} avatar` : 'avatar'} {src} />
                                    {/if}
                                {/await}
                                <Avatar.Fallback class="rounded-lg">{gravatar.initials}</Avatar.Fallback>
                            </Avatar.Root>
                            <div class="grid flex-1 text-left text-sm leading-tight">
                                <span class="truncate font-semibold">{user?.full_name}</span>
                                <span class="truncate text-xs">{user?.email_address}</span>
                            </div>
                            <ChevronsUpDown class="ml-auto size-4" />
                        </Sidebar.MenuButton>
                    {/snippet}
                </DropdownMenu.Trigger>
                <DropdownMenu.Content
                    class="w-(--bits-dropdown-menu-anchor-width) min-w-56 rounded-lg"
                    side={sidebar.isMobile ? 'bottom' : 'right'}
                    align="end"
                    sideOffset={4}
                >
                    <DropdownMenu.Label class="p-0 font-normal">
                        <div class="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
                            <Avatar.Root class="size-8 rounded-lg" title="Profile Image">
                                {#await gravatar.src}
                                    <Avatar.Fallback class="rounded-lg">{gravatar.initials}</Avatar.Fallback>
                                {:then src}
                                    {#if src}
                                        <Avatar.Image alt={user ? `${user.full_name} avatar` : 'avatar'} {src} />
                                    {/if}
                                {/await}
                                <Avatar.Fallback class="rounded-lg">{gravatar.initials}</Avatar.Fallback>
                            </Avatar.Root>
                            <div class="grid flex-1 text-left text-sm leading-tight">
                                <span class="truncate font-semibold">{user?.full_name}</span>
                                <span class="truncate text-xs">{user?.email_address}</span>
                            </div>
                        </div>
                    </DropdownMenu.Label>
                    <DropdownMenu.Separator />

                    <DropdownMenu.Group>
                        <DropdownMenu.Item onSelect={() => navigateTo(resolve('/(app)/account/manage'))}>
                            <BadgeCheck />
                            <span class="w-full">Account</span>
                        </DropdownMenu.Item>
                        <DropdownMenu.Item onSelect={() => navigateTo(resolve('/(app)/account/notifications'))}>
                            <Bell />
                            <span class="w-full">Notifications</span>
                        </DropdownMenu.Item>
                        {#if currentOrganizationId}
                            <DropdownMenu.Item
                                onSelect={() => navigateTo(resolve('/(app)/organization/[organizationId]/manage', { organizationId: currentOrganizationId }))}
                            >
                                <Settings />
                                <span class="w-full">Manage Organization</span>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item
                                onSelect={() => navigateTo(resolve('/(app)/organization/[organizationId]/billing', { organizationId: currentOrganizationId }))}
                            >
                                <CreditCard />
                                <span class="w-full">Billing</span>
                            </DropdownMenu.Item>
                        {:else}
                            <DropdownMenu.Item onSelect={() => navigateTo(resolve('/(app)/organization/add'))}>
                                <Plus />
                                <span class="w-full">Add Organization</span>
                            </DropdownMenu.Item>
                        {/if}
                    </DropdownMenu.Group>
                    <DropdownMenu.Sub>
                        <DropdownMenu.SubTrigger>
                            <BookOpen />
                            Help
                        </DropdownMenu.SubTrigger>
                        <DropdownMenu.SubContent>
                            {#if isChatEnabled}
                                <DropdownMenu.Item class="gap-2 p-2" onSelect={onChatClick}>
                                    <Help />
                                    <span class="font-medium">Support</span>
                                    {#if intercomUnreadCount > 0}
                                        <Badge class="ml-auto shrink-0" variant="secondary">{getUnreadCountLabel(intercomUnreadCount)}</Badge>
                                    {/if}
                                </DropdownMenu.Item>
                            {/if}
                            <DropdownMenu.Item onSelect={() => openExternalLink(documentationHref)}>
                                <BookOpen />
                                <span class="w-full">Documentation</span>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item onSelect={() => openExternalLink(supportIssuesHref)}>
                                <Help />
                                <span class="w-full">Support</span>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item onSelect={() => openExternalLink(githubRepositoryHref)}>
                                <GitHubIcon />
                                <span class="w-full">GitHub</span>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item onSelect={() => openExternalLink(apiReferenceHref)}>
                                <Braces />
                                <span class="w-full">API Reference</span>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item onSelect={onKeyboardShortcutsClick}>
                                <BookOpen />
                                <span class="w-full">Keyboard Shortcuts</span>
                            </DropdownMenu.Item>
                        </DropdownMenu.SubContent>
                    </DropdownMenu.Sub>
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item onSelect={onLogout}>
                        <LogOut />
                        <span class="w-full">Log Out</span>
                    </DropdownMenu.Item>
                </DropdownMenu.Content>
            </DropdownMenu.Root>
        </Sidebar.MenuItem>
    </Sidebar.Menu>
{/if}
