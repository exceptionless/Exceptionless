<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { MultiselectFacetedFilter } from '$comp/faceted-filter';
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

<MultiselectFacetedFilter {title} bind:values={filter.value} options={eventTypes} on:changed={onChanged} on:remove={onRemove}></MultiselectFacetedFilter>
