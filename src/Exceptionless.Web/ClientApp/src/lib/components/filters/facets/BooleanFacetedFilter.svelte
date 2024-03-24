<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    import { BooleanFilter } from '$comp/filters/filters';
    import BooleanFacetedFilter from './base/BooleanFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: BooleanFilter;
    export let title: string;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<BooleanFacetedFilter {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></BooleanFacetedFilter>
