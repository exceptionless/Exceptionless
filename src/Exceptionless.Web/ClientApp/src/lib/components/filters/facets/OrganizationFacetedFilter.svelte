<script lang="ts">
    import { getOrganizationQuery } from '$api/organizationsApi';
    import { OrganizationFilter, type IFilter } from '$comp/filters/filters';
    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: OrganizationFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, title = 'Status', filterChanged, filterRemoved, ...props }: Props = $props();

    const response = getOrganizationQuery();
    const options = $derived(
        $response.data?.map((organization) => ({
            value: organization.id!,
            label: organization.name!
        })) ?? []
    );

    // UPGRADE
    response.subscribe(($response) => {
        if (!$response.isSuccess || !filter.value) {
            return;
        }

        const organization = $response.data.find((organization) => organization.id === filter.value);
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
    loading={$response.isLoading}
    noOptionsText="No organizations found."
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></DropDownFacetedFilter>
