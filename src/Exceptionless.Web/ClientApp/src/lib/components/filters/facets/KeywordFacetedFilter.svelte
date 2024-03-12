<script lang="ts">
    import { KeywordFacetedFilter } from '$comp/faceted-filter';
    import { KeywordFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    export let filter: KeywordFilter;
    export let title: string = 'Keyword';

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<KeywordFacetedFilter {title} bind:value={filter.value} on:changed={onChanged} on:remove={onRemove}></KeywordFacetedFilter>
