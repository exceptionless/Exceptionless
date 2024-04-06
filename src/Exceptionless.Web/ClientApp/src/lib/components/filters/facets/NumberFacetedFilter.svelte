<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Writable } from 'svelte/store';

    import { NumberFilter } from '$comp/filters/filters';
    import NumberFacetedFilter from './base/NumberFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: NumberFilter;
    export let title: string;
    export let open: Writable<boolean>;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<NumberFacetedFilter {open} {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></NumberFacetedFilter>
