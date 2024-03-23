<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { eventTypes } from '$comp/events/options';
    import { TypeFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

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
