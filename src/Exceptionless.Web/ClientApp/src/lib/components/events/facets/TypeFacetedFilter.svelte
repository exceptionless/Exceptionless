<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { DropdownFacetedFilter } from '$comp/filters/facets';
    import { TypeFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    export let filter: TypeFilter;
    export let title: string = 'Type';

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<DropdownFacetedFilter {title} bind:values={filter.values} options={eventTypes} on:changed={onChanged} on:remove={onRemove}></DropdownFacetedFilter>
