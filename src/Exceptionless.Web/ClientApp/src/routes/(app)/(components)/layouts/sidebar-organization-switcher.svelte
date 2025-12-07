<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { A } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar/index';
    import * as DropdownMenu from '$comp/ui/dropdown-menu/index';
    import * as Sidebar from '$comp/ui/sidebar/index';
    import { useSidebar } from '$comp/ui/sidebar/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import ImpersonateOrganizationDialog from '$features/organizations/components/dialogs/impersonate-organization-dialog.svelte';
    import { ViewOrganization } from '$features/organizations/models';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import { getInitials } from '$shared/strings';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';
    import Plus from '@lucide/svelte/icons/plus';
    import Settings from '@lucide/svelte/icons/settings';
    import UserRoundSearch from '@lucide/svelte/icons/user-round-search';

    type Props = HTMLAttributes<HTMLUListElement> & {
        currentOrganizationId: string | undefined;
        impersonatedOrganization: undefined | ViewOrganization;
        isLoading: boolean;
        organizations: undefined | ViewOrganization[];
    };

    let { class: className, currentOrganizationId = $bindable(), impersonatedOrganization, isLoading, organizations = [] }: Props = $props();

    const sidebar = useSidebar();
    const activeOrganization = $derived(impersonatedOrganization ?? organizations.find((organization) => organization.id === currentOrganizationId));
    const isImpersonating = $derived(!!impersonatedOrganization);
    let openImpersonateDialog = $state(false);

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
        await goto(resolve('/(app)'));
        currentOrganizationId = organization.id;
    }

    async function stopImpersonating(): Promise<void> {
        await goto(resolve('/(app)'));
        currentOrganizationId = organizations[0]?.id;
    }
</script>

{#if organizations.length > 0 || isImpersonating}
    <Sidebar.Menu class={className}>
        <Sidebar.MenuItem>
            <DropdownMenu.Root>
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
                    class="w-(--bits-dropdown-menu-anchor-width) min-w-64 rounded-lg"
                    align="start"
                    side={sidebar.isMobile ? 'bottom' : 'right'}
                    sideOffset={4}
                >
                    <DropdownMenu.Label class="text-muted-foreground text-xs">Organizations</DropdownMenu.Label>
                    {#if organizations.length > 0}
                        {#each organizations as organization, index (organization.name)}
                            <DropdownMenu.Item
                                onSelect={() => onOrganizationSelected(organization)}
                                data-active={organization.id === currentOrganizationId && !isImpersonating}
                                class="data-[active=true]:bg-accent data-[active=true]:text-accent-foreground gap-2 p-2"
                            >
                                <Avatar.Root class="size-6 rounded-lg border" title={organization.name}>
                                    <Avatar.Fallback class="rounded-lg">{getInitials(organization.name)}</Avatar.Fallback>
                                </Avatar.Root>
                                {organization.name}
                                <DropdownMenu.Shortcut>⌘{index + 1}</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        {/each}
                    {:else}
                        <DropdownMenu.Item class="text-muted-foreground gap-2 p-2" disabled>
                            <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                <UserRoundSearch class="size-4" aria-hidden="true" />
                            </div>
                            No organizations available
                        </DropdownMenu.Item>
                    {/if}
                    <DropdownMenu.Separator />
                    {#if activeOrganization?.id}
                        <DropdownMenu.Item>
                            <A
                                variant="ghost"
                                href={resolve('/(app)/organization/[organizationId]/manage', { organizationId: activeOrganization.id })}
                                class="flex w-full items-center gap-2"
                            >
                                <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                    <Settings class="size-4" aria-hidden="true" />
                                </div>
                                <span class="text-muted-foreground font-medium">Manage organization</span>
                                <DropdownMenu.Shortcut>⇧⌘go</DropdownMenu.Shortcut>
                            </A>
                        </DropdownMenu.Item>
                    {/if}
                    <DropdownMenu.Item>
                        <A variant="ghost" href={resolve('/(app)/organization/add')} class="flex w-full items-center gap-2">
                            <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                                <Plus class="size-4" aria-hidden="true" />
                            </div>
                            <span class="text-muted-foreground font-medium">Add organization</span>
                            <DropdownMenu.Shortcut>⇧⌘gn</DropdownMenu.Shortcut>
                        </A>
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
