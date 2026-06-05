<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { HTMLAttributes } from 'svelte/elements';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import DelayedRender from '$comp/delayed-render.svelte';
    import * as Avatar from '$comp/ui/avatar/index';
    import * as DropdownMenu from '$comp/ui/dropdown-menu/index';
    import * as Sidebar from '$comp/ui/sidebar/index';
    import { useSidebar } from '$comp/ui/sidebar/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import ImpersonateOrganizationDialog from '$features/organizations/components/dialogs/impersonate-organization-dialog.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import { getInitials } from '$shared/strings';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';
    import Plus from '@lucide/svelte/icons/plus';
    import Settings from '@lucide/svelte/icons/settings';
    import UserRoundSearch from '@lucide/svelte/icons/user-round-search';
    import { tick } from 'svelte';

    type Props = HTMLAttributes<HTMLUListElement> & {
        currentOrganizationId: string | undefined;
        impersonatedOrganization: undefined | ViewOrganization;
        isGlobalAdmin: boolean;
        isLoading: boolean;
        open?: boolean;
        organizations: undefined | ViewOrganization[];
    };

    let {
        class: className,
        currentOrganizationId = $bindable(),
        impersonatedOrganization,
        isGlobalAdmin,
        isLoading,
        open = $bindable(false),
        organizations = []
    }: Props = $props();

    const sidebar = useSidebar();
    const activeOrganization = $derived(impersonatedOrganization ?? organizations.find((organization) => organization.id === currentOrganizationId));
    const isImpersonating = $derived(!!impersonatedOrganization);
    const useSingleOrganizationShortcut = $derived(!isGlobalAdmin && !isImpersonating && organizations.length === 1 && !!activeOrganization?.id);
    let menuContentElement = $state<HTMLElement | null>(null);
    let openImpersonateDialog = $state(false);

    $effect(() => {
        if (open) {
            void focusActiveOrganizationItem();
        }
    });

    async function focusActiveOrganizationItem(): Promise<void> {
        await tick();
        await new Promise<void>((resolve) => window.setTimeout(resolve));
        (
            menuContentElement?.querySelector<HTMLElement>('[data-current-organization="true"]') ??
            menuContentElement?.querySelector<HTMLElement>('[data-slot="dropdown-menu-item"]')
        )?.focus();
    }

    function onOrganizationSelected(organization: ViewOrganization): void {
        if (sidebar.isMobile) {
            sidebar.toggle();
        }

        if (organization.id === currentOrganizationId) {
            return;
        }

        currentOrganizationId = organization.id;
    }

    async function handleImpersonate(organization: ViewOrganization): Promise<void> {
        await goto(resolve('/(app)/stack'));
        currentOrganizationId = organization.id;
    }

    async function stopImpersonating(): Promise<void> {
        await goto(resolve('/(app)/stack'));
        currentOrganizationId = organizations[0]?.id;
    }

    async function navigateTo(href: string): Promise<void> {
        await goto(href);
    }
</script>

{#if organizations.length > 0 || isImpersonating}
    <Sidebar.Menu class={className}>
        <Sidebar.MenuItem>
            {#if useSingleOrganizationShortcut && activeOrganization?.id}
                <Sidebar.MenuButton
                    size="lg"
                    class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
                    onclick={() => void navigateTo(resolve('/(app)/organization/[organizationId]/manage', { organizationId: activeOrganization.id }))}
                >
                    <Avatar.Root class="size-8 rounded-lg border" title="Organization Icon">
                        {#if activeOrganization.icon_url}
                            <Avatar.Image alt={`${activeOrganization.name} icon`} src={activeOrganization.icon_url} />
                        {/if}
                        <Avatar.Fallback class="rounded-lg">{getInitials(activeOrganization.name)}</Avatar.Fallback>
                    </Avatar.Root>
                    <div class="grid flex-1 text-left text-sm leading-tight">
                        <span class="truncate font-semibold">Organization</span>
                        <span class="truncate text-xs">{activeOrganization.name}</span>
                    </div>
                    <Settings class="ml-auto" />
                </Sidebar.MenuButton>
            {:else}
                <DropdownMenu.Root bind:open>
                    <DropdownMenu.Trigger>
                        {#snippet child({ props })}
                            <Sidebar.MenuButton
                                {...props}
                                size="lg"
                                class={[
                                    'data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground',
                                    isImpersonating && 'bg-violet-100 dark:bg-violet-900/30'
                                ]}
                            >
                                <Avatar.Root class={['size-8 rounded-lg border', isImpersonating && 'border-violet-500']} title="Organization Avatar">
                                    {#if activeOrganization?.icon_url}
                                        <Avatar.Image alt={`${activeOrganization.name} icon`} src={activeOrganization.icon_url} />
                                    {/if}
                                    <Avatar.Fallback
                                        class={['rounded-lg', isImpersonating && 'bg-violet-200 text-violet-900 dark:bg-violet-800 dark:text-violet-100']}
                                    >
                                        {getInitials(activeOrganization?.name ?? '?')}
                                    </Avatar.Fallback>
                                </Avatar.Root>
                                <div class="grid flex-1 text-left text-sm leading-tight">
                                    <span class="truncate font-semibold">
                                        {activeOrganization?.name ?? 'Select an organization'}
                                    </span>
                                    <span class={['truncate text-xs', isImpersonating && 'text-violet-600 dark:text-violet-400']}>
                                        {#if isImpersonating}
                                            Impersonating
                                        {:else}
                                            <span class="truncate text-xs">{activeOrganization?.plan_name ?? 'No organization selected'}</span>
                                        {/if}
                                    </span>
                                </div>
                                <ChevronsUpDown class="ml-auto" />
                            </Sidebar.MenuButton>
                        {/snippet}
                    </DropdownMenu.Trigger>
                    <DropdownMenu.Content
                        bind:ref={menuContentElement}
                        class="w-(--bits-dropdown-menu-anchor-width) min-w-64 rounded-lg"
                        align="start"
                        side={sidebar.isMobile ? 'bottom' : 'right'}
                        sideOffset={4}
                    >
                        <DropdownMenu.Label class="text-muted-foreground text-xs">Organizations</DropdownMenu.Label>
                        {#if organizations.length > 0}
                            {#each organizations as organization (organization.name)}
                                <DropdownMenu.Item
                                    onSelect={() => onOrganizationSelected(organization)}
                                    data-current-organization={organization.id === currentOrganizationId && !isImpersonating ? 'true' : undefined}
                                    class="gap-2 p-2"
                                >
                                    <Avatar.Root class="size-6 rounded-lg border" title={organization.name}>
                                        {#if organization.icon_url}
                                            <Avatar.Image alt={`${organization.name} icon`} src={organization.icon_url} />
                                        {/if}
                                        <Avatar.Fallback class="rounded-lg">{getInitials(organization.name)}</Avatar.Fallback>
                                    </Avatar.Root>
                                    {organization.name}
                                </DropdownMenu.Item>
                            {/each}
                        {:else}
                            <DropdownMenu.Item class="text-muted-foreground gap-2 p-2" disabled>
                                <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                    <UserRoundSearch class="size-4" aria-hidden="true" />
                                </div>
                                No Organizations Available
                            </DropdownMenu.Item>
                        {/if}
                        <DropdownMenu.Separator />
                        {#if activeOrganization?.id}
                            <DropdownMenu.Item
                                onSelect={() =>
                                    void navigateTo(resolve('/(app)/organization/[organizationId]/manage', { organizationId: activeOrganization.id }))}
                            >
                                <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                    <Settings class="size-4" aria-hidden="true" />
                                </div>
                                <span class="text-muted-foreground font-medium">Manage Organization</span>
                            </DropdownMenu.Item>
                        {/if}
                        <DropdownMenu.Item onSelect={() => void navigateTo(resolve('/(app)/organization/add'))}>
                            <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                <Plus class="size-4" aria-hidden="true" />
                            </div>
                            <span class="text-muted-foreground font-medium">Add Organization</span>
                        </DropdownMenu.Item>
                        <GlobalUser>
                            <DropdownMenu.Separator />
                            <DropdownMenu.Label class="text-muted-foreground text-xs">Admin</DropdownMenu.Label>
                            {#if isImpersonating}
                                <DropdownMenu.Item onSelect={stopImpersonating} class="gap-2 p-2 text-violet-600 dark:text-violet-400">
                                    <div
                                        class="flex size-6 items-center justify-center rounded-md border border-violet-300 bg-violet-100 dark:border-violet-700 dark:bg-violet-900/50"
                                    >
                                        <EyeOff class="size-4" aria-hidden="true" />
                                    </div>
                                    <span class="font-medium">Stop Impersonating</span>
                                </DropdownMenu.Item>
                            {:else}
                                <DropdownMenu.Item onSelect={() => (openImpersonateDialog = true)} class="gap-2 p-2">
                                    <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                        <Eye class="size-4" aria-hidden="true" />
                                    </div>
                                    <span class="text-muted-foreground font-medium">Impersonate Organization</span>
                                </DropdownMenu.Item>
                            {/if}
                        </GlobalUser>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            {/if}
        </Sidebar.MenuItem>
    </Sidebar.Menu>
{:else if isLoading}
    <DelayedRender>
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
    </DelayedRender>
{/if}

{#if openImpersonateDialog}
    <ImpersonateOrganizationDialog
        bind:open={openImpersonateDialog}
        impersonateOrganization={handleImpersonate}
        userOrganizationIds={organizations.map((o) => o.id)}
    />
{/if}
