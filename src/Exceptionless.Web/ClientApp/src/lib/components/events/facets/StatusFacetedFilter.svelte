<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { FacetedFilterDropdown } from '$comp/facets';
    import { StatusFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    export let filter: StatusFilter;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<FacetedFilterDropdown title="Status" bind:values={filter.values} options={stackStatuses} on:changed={onChanged} on:remove={onRemove}></FacetedFilterDropdown>
