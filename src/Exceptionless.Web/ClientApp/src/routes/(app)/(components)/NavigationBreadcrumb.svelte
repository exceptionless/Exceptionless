<script lang="ts">
    import IconChevronDown from '~icons/mdi/chevron-down';
    import IconSeparator from '~icons/mdi/slash-forward';

    import * as Breadcrumb from '$comp/ui/breadcrumb/index.js';
    import * as DropdownMenu from '$comp/ui/dropdown-menu/index.js';

    import { getOrganizationQuery } from '$api/organizationsApi';
    import type { ViewOrganization } from '$lib/models/api';
    import Loading from '$comp/Loading.svelte';

    const response = getOrganizationQuery();

    let selectedOrganization = $response.data?.[0];
    function onOrganizationChange(organization: ViewOrganization) {
        selectedOrganization = organization;
    }
</script>

<div class="pb-4">
    <Breadcrumb.Root>
        <Breadcrumb.List class="text-xl">
            {#if $response.isLoading}
                <Breadcrumb.Item>
                    <Loading />
                </Breadcrumb.Item>
            {:else}
                <DropdownMenu.Root>
                    <DropdownMenu.Trigger class="flex items-center gap-1">
                        {selectedOrganization?.name}
                        <IconChevronDown tabindex={-1} />
                    </DropdownMenu.Trigger>
                    <DropdownMenu.Content align="start">
                        <DropdownMenu.Label>Organization</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.RadioGroup value={selectedOrganization?.value}>
                            {#each $response.data as organization (organization.id)}
                                <DropdownMenu.RadioItem value={organization.id} on:click={() => onOrganizationChange(organization)}
                                    >{organization.name}</DropdownMenu.RadioItem
                                >
                            {/each}
                        </DropdownMenu.RadioGroup>
                        <slot></slot>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            {/if}

            <Breadcrumb.Separator>
                <IconSeparator />
            </Breadcrumb.Separator>
            <Breadcrumb.Item>
                <Breadcrumb.Link href="/next/">Events</Breadcrumb.Link>
            </Breadcrumb.Item>
        </Breadcrumb.List>
    </Breadcrumb.Root>
</div>
