<script lang="ts">
    import { KeywordFilter } from '$comp/filters/filters.svelte';
    import KeywordFacetedFilter from './base/KeywordFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, title = 'Keyword', filterChanged, filterRemoved, ...props }: FacetedFilterProps<KeywordFilter> = $props();
</script>

<KeywordFacetedFilter
    {title}
    value={filter.value}
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {...props}
></KeywordFacetedFilter>
