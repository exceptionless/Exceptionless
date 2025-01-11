<script lang="ts">
    import { KeywordFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import KeywordFacetedFilter from './base/keyword-faceted-filter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Keyword', ...props }: FacetedFilterProps<KeywordFilter> = $props();
</script>

<KeywordFacetedFilter
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {title}
    value={filter.value}
    {...props}
></KeywordFacetedFilter>
