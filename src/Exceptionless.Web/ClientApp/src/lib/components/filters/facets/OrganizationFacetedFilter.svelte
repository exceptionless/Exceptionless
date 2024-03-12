<script lang="ts">
    import { getOrganizationQuery } from '$api/organizationsApi';
    import { DropdownFacetedFilter } from '$comp/faceted-filter';
    import { OrganizationFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';
    import { derived } from 'svelte/store';

    const dispatch = createEventDispatcher();
    export let filter: OrganizationFilter;
    export let title: string = 'Status';

    const response = getOrganizationQuery();
    const options = derived(response, ($response) => {
        return (
            $response.data?.map((organization) => ({
                value: organization.id!,
                label: organization.name!
            })) ?? []
        );
    });

    response.subscribe(($response) => {
        if (!$response.isSuccess || !filter.value) {
            return;
        }

        const organization = $response.data.find((organization) => organization.id === filter.value);
        if (!organization) {
            filter.value = '';
            dispatch('changed', filter);
        }
    });

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<DropdownFacetedFilter {title} bind:value={filter.value} options={$options} loading={$response.isLoading} on:changed={onChanged} on:remove={onRemove}
></DropdownFacetedFilter>
