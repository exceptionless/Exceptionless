<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    import { SessionFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: SessionFilter;
    export let title: string = 'Session';

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<StringFacetedFilter {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></StringFacetedFilter>
