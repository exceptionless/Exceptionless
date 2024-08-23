<script lang="ts">
    import { OrganizationFilter } from '$comp/filters/filters.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';

    import type { FacetedFilterProps } from '.';

    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Status', ...props }: FacetedFilterProps<OrganizationFilter> = $props();

    const response = getOrganizationQuery({ mode: 'stats' });
    const options = $derived(
        response.data?.map((organization) => ({
            label: organization.name!,
            value: organization.id!
        })) ?? []
    );

    $effect(() => {
        if (!response.isSuccess || !filter.value) {
            return;
        }

        const organization = response.data.find((organization) => organization.id === filter.value);
        if (!organization) {
            filter.value = undefined;
            filterChanged(filter);
        }
    });
</script>

<DropDownFacetedFilter
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    loading={response.isLoading}
    noOptionsText="No organizations found."
    {options}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {title}
    value={filter.value}
    {...props}
></DropDownFacetedFilter>
