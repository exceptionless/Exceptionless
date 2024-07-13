<script lang="ts">
    import { getOrganizationQuery } from '$api/organizationsApi.svelte';
    import { OrganizationFilter } from '$comp/filters/filters.svelte';
    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, title = 'Status', filterChanged, filterRemoved, ...props }: FacetedFilterProps<OrganizationFilter> = $props();

    const response = getOrganizationQuery({ mode: 'stats' });
    const options = $derived(
        response.data?.map((organization) => ({
            value: organization.id!,
            label: organization.name!
        })) ?? []
    );

    $effect(() => {
        if (!response.isSuccess || !filter.value) {
            return;
        }

        const organization = response.data.find((organization) => organization.id === filter.value);
        if (!organization) {
            filter.value = '';
            filterChanged(filter);
        }
    });
</script>

<DropDownFacetedFilter
    {title}
    bind:value={filter.value}
    {options}
    loading={response.isLoading}
    noOptionsText="No organizations found."
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></DropDownFacetedFilter>
