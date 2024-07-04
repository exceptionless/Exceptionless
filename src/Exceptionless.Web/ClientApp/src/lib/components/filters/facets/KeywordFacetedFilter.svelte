<script lang="ts">
    import { KeywordFilter } from '$comp/filters/filters';
    import KeywordFacetedFilter from './base/KeywordFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, title = 'Keyword', filterChanged, filterRemoved, ...props }: FacetedFilterProps<KeywordFilter> = $props();
</script>

<KeywordFacetedFilter {title} bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}
></KeywordFacetedFilter>
