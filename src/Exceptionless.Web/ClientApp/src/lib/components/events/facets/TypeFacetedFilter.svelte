<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { FacetedFilterDropdown } from '$comp/facets';
    import { TypeFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    export let filter: TypeFilter;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<FacetedFilterDropdown title="Type" bind:values={filter.values} options={eventTypes} on:changed={onChanged} on:remove={onRemove}></FacetedFilterDropdown>
