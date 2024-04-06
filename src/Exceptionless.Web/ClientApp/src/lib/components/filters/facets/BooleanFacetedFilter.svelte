<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Writable } from 'svelte/store';

    import { BooleanFilter } from '$comp/filters/filters';
    import BooleanFacetedFilter from './base/BooleanFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: BooleanFilter;
    export let title: string;
    export let open: Writable<boolean>;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<BooleanFacetedFilter {open} {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></BooleanFacetedFilter>
