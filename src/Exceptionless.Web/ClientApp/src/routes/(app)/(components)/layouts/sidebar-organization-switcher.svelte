<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import DelayedRender from '$comp/delayed-render.svelte';
    import * as Avatar from '$comp/ui/avatar/index';
    import * as DropdownMenu from '$comp/ui/dropdown-menu/index';
    import * as Sidebar from '$comp/ui/sidebar/index';
    import { useSidebar } from '$comp/ui/sidebar/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import { ViewOrganization } from '$features/organizations/models';
    import { getInitials } from '$shared/strings';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import Plus from '@lucide/svelte/icons/plus';

    type Props = HTMLAttributes<HTMLUListElement> & {
        isLoading: boolean;
        organizations: undefined | ViewOrganization[];
        selected: string | undefined;
    };

    let { class: className, isLoading, organizations, selected = $bindable() }: Props = $props();

    const sidebar = useSidebar();
    let activeOrganization = $derived(organizations?.find((organization) => organization.id === selected));

    function onOrganizationSelected(organization: ViewOrganization): void {
        if (sidebar.isMobile) {
            sidebar.toggle();
        }

        if (organization.id === selected) {
            return;
        }

        selected = organization.id;
    }
</script>

{#if organizations && organizations.length > 1}
    <Sidebar.Menu class={className}>
        <Sidebar.MenuItem>
            <DropdownMenu.Root>
                <DropdownMenu.Trigger>
                    {#snippet child({ props })}
                        <Sidebar.MenuButton {...props} size="lg" class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground">
                            <Avatar.Root class="size-8 rounded-lg border" title="Organization Avatar">
                                <Avatar.Fallback class="rounded-lg">{getInitials(activeOrganization?.name)}</Avatar.Fallback>
                            </Avatar.Root>
                            <div class="grid flex-1 text-left text-sm leading-tight">
                                <span class="truncate font-semibold">
                                    {activeOrganization?.name}
                                </span>
                                <span class="truncate text-xs">{activeOrganization?.plan_name}</span>
                            </div>
                            <ChevronsUpDown class="ml-auto" />
                        </Sidebar.MenuButton>
                    {/snippet}
                </DropdownMenu.Trigger>
                <DropdownMenu.Content
                    class="w-(--bits-dropdown-menu-anchor-width) min-w-56 rounded-lg"
                    align="start"
                    side={sidebar.isMobile ? 'bottom' : 'right'}
                    sideOffset={4}
                >
                    <DropdownMenu.Label class="text-muted-foreground text-xs">Organizations</DropdownMenu.Label>
                    {#if organizations}
                        {#each organizations as organization, index (organization.name)}
                            <DropdownMenu.Item
                                onSelect={() => onOrganizationSelected(organization)}
                                data-active={organization.id === selected}
                                class="data-[active=true]:bg-accent data-[active=true]:text-accent-foreground gap-2 p-2"
                            >
                                <Avatar.Root class="size-6 rounded-lg border" title={organization.name}>
                                    <Avatar.Fallback class="rounded-lg">{getInitials(organization.name)}</Avatar.Fallback>
                                </Avatar.Root>
                                {organization.name}
                                <DropdownMenu.Shortcut>⌘{index + 1}</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        {/each}
                    {/if}
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item class="gap-2 p-2">
                        <div class="bg-background flex size-6 items-center justify-center rounded-md border">
                            <Plus class="size-4" aria-hidden="true" />
                        </div>
                        <span class="text-muted-foreground font-medium">Add organization</span>
                    </DropdownMenu.Item>
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
