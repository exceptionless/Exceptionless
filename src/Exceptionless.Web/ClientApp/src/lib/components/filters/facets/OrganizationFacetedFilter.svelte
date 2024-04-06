<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { derived, type Writable } from 'svelte/store';

    import { getOrganizationQuery } from '$api/organizationsApi';
    import { OrganizationFilter } from '$comp/filters/filters';
    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: OrganizationFilter;
    export let title: string = 'Status';
    export let open: Writable<boolean>;

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

<DropDownFacetedFilter
    {open}
    {title}
    bind:value={filter.value}
    options={$options}
    loading={$response.isLoading}
    noOptionsText="No organizations found."
    on:changed={onChanged}
    on:remove={onRemove}
></DropDownFacetedFilter>
