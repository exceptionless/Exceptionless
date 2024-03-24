<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    import { NumberFilter } from '$comp/filters/filters';
    import NumberFacetedFilter from './base/NumberFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: NumberFilter;
    export let title: string;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<NumberFacetedFilter {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></NumberFacetedFilter>
