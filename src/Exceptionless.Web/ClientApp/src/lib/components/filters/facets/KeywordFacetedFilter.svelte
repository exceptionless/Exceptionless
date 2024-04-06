<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Writable } from 'svelte/store';

    import { KeywordFilter } from '$comp/filters/filters';
    import KeywordFacetedFilter from './base/KeywordFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: KeywordFilter;
    export let title: string = 'Keyword';
    export let open: Writable<boolean>;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<KeywordFacetedFilter {open} {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></KeywordFacetedFilter>
