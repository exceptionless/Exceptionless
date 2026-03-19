<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { Gravatar } from '$features/users/gravatar.svelte';
    import type { ViewCurrentUser } from '$features/users/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { A } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar/index';
    import { Badge } from '$comp/ui/badge';
    import * as DropdownMenu from '$comp/ui/dropdown-menu/index';
    import * as Sidebar from '$comp/ui/sidebar/index';
    import { useSidebar } from '$comp/ui/sidebar/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import ImpersonateOrganizationDialog from '$features/organizations/components/dialogs/impersonate-organization-dialog.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { apiReferenceHref, documentationHref, githubRepositoryHref, supportIssuesHref } from '$features/shared/help-links';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import BadgeCheck from '@lucide/svelte/icons/badge-check';
    import Bell from '@lucide/svelte/icons/bell';
    import BookOpen from '@lucide/svelte/icons/book-open';
    import Braces from '@lucide/svelte/icons/braces';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import Help from '@lucide/svelte/icons/circle-help';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import Database from '@lucide/svelte/icons/database';
    import DatabaseZap from '@lucide/svelte/icons/database-zap';
    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';
    import GitHub from '@lucide/svelte/icons/github';
    import LayoutDashboard from '@lucide/svelte/icons/layout-dashboard';
    import LogOut from '@lucide/svelte/icons/log-out';
    import Play from '@lucide/svelte/icons/play';
    import Plus from '@lucide/svelte/icons/plus';
    import Settings from '@lucide/svelte/icons/settings';
    import Wrench from '@lucide/svelte/icons/wrench';

    interface Props {
        gravatar: Gravatar;
        intercomUnreadCount: number;
        isChatEnabled: boolean;
        isImpersonating?: boolean;
        isLoading: boolean;
        openChat: () => void;
        organizations?: ViewOrganization[];
        user: undefined | ViewCurrentUser;
    }

    let { gravatar, intercomUnreadCount = 0, isChatEnabled, isImpersonating = false, isLoading, openChat, organizations = [], user }: Props = $props();
    const sidebar = useSidebar();
    let openImpersonateDialog = $state(false);

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

    async function impersonateOrganization(vo: ViewOrganization): Promise<void> {
        await goto(resolve('/(app)'));
        organization.current = vo.id;
    }

    async function stopImpersonating(): Promise<void> {
        await goto(resolve('/(app)'));
        organization.current = organizations[0]?.id;
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
                        <span class="text-sm leading-none font-medium">Chat with support</span>
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
            <DropdownMenu.Root>
                <DropdownMenu.Trigger>
                    {#snippet child({ props })}
                        <Sidebar.MenuButton size="lg" class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground" {...props}>
                            <Avatar.Root class="size-8 rounded-lg" title="Profile Image">
                                {#await gravatar.src}
                                    <Avatar.Fallback class="rounded-lg">{gravatar.initials}</Avatar.Fallback>
                                {:then src}
                                    <Avatar.Image alt={user ? `${user.full_name} avatar` : 'avatar'} {src} />
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
                                    <Avatar.Image alt={user ? `${user.full_name} avatar` : 'avatar'} {src} />
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
                        <DropdownMenu.Item>
                            <BadgeCheck />
                            <A variant="ghost" href={resolve('/(app)/account/manage')} class="w-full" onclick={onMenuClick}>Account</A>
                            <DropdownMenu.Shortcut>⇧⌘ga</DropdownMenu.Shortcut>
                        </DropdownMenu.Item>
                        <DropdownMenu.Item>
                            <Bell />
                            <A variant="ghost" href={resolve('/(app)/account/notifications')} class="w-full" onclick={onMenuClick}>Notifications</A>
                            <DropdownMenu.Shortcut>⇧⌘gn</DropdownMenu.Shortcut>
                        </DropdownMenu.Item>
                        {#if organization.current}
                            <DropdownMenu.Item>
                                <Settings />
                                <A
                                    variant="ghost"
                                    href={resolve('/(app)/organization/[organizationId]/manage', { organizationId: organization.current })}
                                    class="flex w-full items-center gap-2"
                                    onclick={onMenuClick}
                                >
                                    Manage organization
                                    <DropdownMenu.Shortcut>⇧⌘go</DropdownMenu.Shortcut>
                                </A>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item>
                                <CreditCard />
                                <A
                                    variant="ghost"
                                    href={resolve('/(app)/organization/[organizationId]/billing', { organizationId: organization.current })}
                                    class="flex w-full items-center gap-2"
                                    onclick={onMenuClick}
                                >
                                    Billing
                                    <DropdownMenu.Shortcut>⇧⌘gb</DropdownMenu.Shortcut>
                                </A>
                            </DropdownMenu.Item>
                        {:else}
                            <DropdownMenu.Item>
                                <Plus />
                                <A variant="ghost" href={resolve('/(app)/organization/add')} class="flex w-full items-center gap-2" onclick={onMenuClick}>
                                    Add organization
                                    <DropdownMenu.Shortcut>⇧⌘gn</DropdownMenu.Shortcut>
                                </A>
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
                            <DropdownMenu.Item>
                                <BookOpen />
                                <A variant="ghost" href={documentationHref} target="_blank" class="w-full">Documentation</A>
                                <DropdownMenu.Shortcut>⌘gw</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item>
                                <Help />
                                <A variant="ghost" href={supportIssuesHref} target="_blank" class="w-full">Support</A>
                                <DropdownMenu.Shortcut>⌘gs</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item>
                                <GitHub />
                                <A variant="ghost" href={githubRepositoryHref} target="_blank" class="w-full">GitHub</A>
                                <DropdownMenu.Shortcut>⌘gg</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item>
                                <Braces />
                                <A variant="ghost" href={apiReferenceHref} target="_blank" class="w-full">API Reference</A>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item>
                                <BookOpen />
                                Keyboard shortcuts
                                <DropdownMenu.Shortcut>⌘K</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        </DropdownMenu.SubContent>
                    </DropdownMenu.Sub>
                    <GlobalUser>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Sub>
                            <DropdownMenu.SubTrigger>
                                <Wrench />
                                System
                            </DropdownMenu.SubTrigger>
                            <DropdownMenu.SubContent>
                                <DropdownMenu.Item>
                                    <LayoutDashboard />
                                    <A variant="ghost" href={resolve('/(app)/system/overview')} class="w-full" onclick={onMenuClick}>Overview</A>
                                </DropdownMenu.Item>
                                <DropdownMenu.Item>
                                    <Database />
                                    <A variant="ghost" href={resolve('/(app)/system/elasticsearch/overview')} class="w-full" onclick={onMenuClick}
                                        >Elasticsearch</A
                                    >
                                </DropdownMenu.Item>
                                <DropdownMenu.Item>
                                    <Play />
                                    <A variant="ghost" href={resolve('/(app)/system/actions')} class="w-full" onclick={onMenuClick}>Actions</A>
                                </DropdownMenu.Item>
                                <DropdownMenu.Item>
                                    <DatabaseZap />
                                    <A variant="ghost" href={resolve('/(app)/system/migrations')} class="w-full" onclick={onMenuClick}>Migrations</A>
                                </DropdownMenu.Item>
                            </DropdownMenu.SubContent>
                        </DropdownMenu.Sub>
                        {#if isImpersonating}
                            <DropdownMenu.Item
                                onSelect={stopImpersonating}
                                class="bg-violet-100 text-violet-900 hover:bg-violet-200 dark:bg-violet-900/30 dark:text-violet-100 dark:hover:bg-violet-900/50"
                            >
                                <EyeOff />
                                Stop Impersonating
                            </DropdownMenu.Item>
                        {:else}
                            <DropdownMenu.Item onSelect={() => (openImpersonateDialog = true)}>
                                <Eye />
                                Impersonate Organization
                            </DropdownMenu.Item>
                        {/if}
                    </GlobalUser>
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item>
                        <LogOut />
                        <A variant="ghost" href={resolve('/(auth)/logout')} class="w-full">Log out</A>
                        <DropdownMenu.Shortcut>⇧⌘Q</DropdownMenu.Shortcut>
                    </DropdownMenu.Item>
                </DropdownMenu.Content>
            </DropdownMenu.Root>
        </Sidebar.MenuItem>
    </Sidebar.Menu>
{/if}

{#if openImpersonateDialog}
    <ImpersonateOrganizationDialog bind:open={openImpersonateDialog} {impersonateOrganization} userOrganizationIds={organizations.map((o) => o.id)} />
{/if}
