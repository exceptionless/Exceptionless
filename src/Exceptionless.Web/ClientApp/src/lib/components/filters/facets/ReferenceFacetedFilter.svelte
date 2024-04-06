<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Writable } from 'svelte/store';

    import { ReferenceFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: ReferenceFilter;
    export let title: string = 'Reference';
    export let open: Writable<boolean>;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<StringFacetedFilter {open} {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></StringFacetedFilter>
