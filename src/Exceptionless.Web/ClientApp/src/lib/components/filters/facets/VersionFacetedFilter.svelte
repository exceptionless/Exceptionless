<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    import { VersionFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: VersionFilter;
    export let title: string;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<StringFacetedFilter {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></StringFacetedFilter>
