<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { stackStatuses } from '$comp/events/options';
    import { StatusFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: StatusFilter;
    export let title: string = 'Status';

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<MultiselectFacetedFilter {title} bind:values={filter.value} options={stackStatuses} on:changed={onChanged} on:remove={onRemove}></MultiselectFacetedFilter>
